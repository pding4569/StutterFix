"""생성기를 실행하되 게임·sfmeasure·설치를 호출하지 않는 계약 검사."""
import importlib.util
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location('render_merge_steps', Path(__file__).with_name('render-merge-steps.py'))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class StepsContract(unittest.TestCase):
    def setUp(self):
        self.plan = module.build(Path('Arche FULL.adofai'))
        self.steps = self.plan['steps']

    def test_abba_same_window(self):
        begins = [s for s in self.steps if s.startswith('gpudeco rendermerge begin ')]
        self.assertEqual(begins, [f'gpudeco rendermerge begin {i}' for i in range(4)])
        for begin in begins:
            k = self.steps.index(begin)
            self.assertEqual(self.steps[k + 1:k + 3], ['waitsongpos 270 30', 'gpudeco rendermerge end'])
            self.assertIn('waitsongpos 260 40', self.steps[k - 4:k])

    def test_abba_activation_never_pauses(self):
        previous = 0
        for arm, merged in enumerate((False, True, True, False)):
            end = self.steps.index(f'gpudeco rendermerge begin {arm}')
            seek = max(i for i in range(previous, end) if self.steps[i] == 'seek 260 12')
            activation = self.steps[seek:end]
            self.assertNotIn('pause', activation)
            self.assertNotIn('resume', activation)
            self.assertEqual(activation[:6], ['seek 260 12', 'gpudeco rendermerge playbackready',
                             'decobake ready', 'waitsongpos 256 40',
                             'gpudeco packets compiled', 'gpudeco live on'])
            self.assertEqual('gpudeco rendermerge ready' in activation, merged)
            self.assertIn('gpudeco rendermerge ' + ('on' if merged else 'off'), activation)
            self.assertIn('gpudeco live census', activation)
            previous = end

    def test_no_zero_play_or_external_execution(self):
        self.assertFalse(any(s == 'play' or s.startswith('seek 0') for s in self.steps))
        self.assertEqual(self.steps.count('gpudeco rendermerge result'), 1)
        self.assertEqual(self.steps[-2:], ['gpudeco rendermerge result', 'quit'])

    def test_pixel_controls_and_cleanup(self):
        for i in range(2):
            k = self.steps.index(f'gpudeco rendermerge pixel {i}')
            self.assertEqual(self.steps[k - 6:k], ['gpudeco rendermerge ready', 'framecmp noise A N', 'gpudeco rendermerge on',
                              'framecmp grab B', 'gpudeco rendermerge off', 'framecmp grab A2'])
        self.assertEqual(self.steps.count('framecmp clear'), 4)
        self.assertIn('gpudeco rendermerge stability song_end', self.steps)
        self.assertIn('retry', self.steps)
        self.assertEqual(sum(s.startswith('open ') for s in self.steps), 2)
        k = self.steps.index('decobake off')
        self.assertEqual(self.steps[k - 2:k], ['pause', 'gpudeco resources mark'])
        self.assertIn('gpudeco resources clean', self.steps[k:])

    def test_optional_mono_same_process(self):
        steps = module.build(Path('Arche FULL.adofai'), mono_dir=Path('trace folder with spaces'))['steps']
        k = steps.index('trace rendermerge-managed 8')
        syms = next(i for i, s in enumerate(steps) if s.startswith('monosyms '))
        self.assertLess(k, syms)
        self.assertTrue(steps[syms].startswith('monosyms "'))
        self.assertEqual(steps[-1], 'quit')
        self.assertNotIn('quit', steps[k:syms])

    def test_environment_checked_before_loading_chart(self):
        self.assertLess(self.steps.index('gpudeco rendermerge reset'),
                        self.steps.index('gpudeco rendermerge environment'))
        self.assertLess(self.steps.index('gpudeco rendermerge environment'),
                        next(i for i, s in enumerate(self.steps) if s.startswith('open ')))

    def test_checkpoint_callback_finishes_before_freezing(self):
        # Round 1: scheduled only observed PrepVfx. OnMusicScheduled could
        # subsequently Scrub and clear AudioListener.pause after our pause.
        for mono in (None, Path('trace folder with spaces')):
            steps = module.build(Path('Arche FULL.adofai'), mono_dir=mono)['steps']
            for i, step in enumerate(steps):
                if step == 'gpudeco scheduled':
                    self.assertEqual(steps[i + 1:i + 4],
                                     ['gpudeco rendermerge playbackready',
                                      'waitsongpos 250 40', 'pause'])

    def test_preparation_and_mode_transition_contract(self):
        for mono in (None, Path('trace folder with spaces')):
            playing = paused = owned = merged = window = playback_ready = False
            for step in module.build(Path('Arche FULL.adofai'), mono_dir=mono)['steps']:
                if step.startswith('seek '):
                    self.assertFalse(playing, step)
                    playing = True
                    playback_ready = False
                elif step == 'gpudeco rendermerge playbackready':
                    self.assertTrue(playing and not paused and not owned, step)
                    playback_ready = True
                elif step == 'pause':
                    self.assertTrue(playing and playback_ready, step)
                    paused = True
                elif step == 'resume':
                    paused = False
                elif step == 'gpudeco live on':
                    self.assertTrue(playing and playback_ready, step)
                    owned = True
                elif step in ('gpudeco live off', 'decobake off'):
                    self.assertFalse(window, step)
                    owned = False
                elif step == 'stop':
                    self.assertFalse(paused or window, step)
                    playing = owned = playback_ready = False
                elif step in ('gpudeco rendermerge on', 'gpudeco rendermerge off'):
                    self.assertFalse(window, step)
                    merged = step.endswith(' on')
                elif step == 'gpudeco rendermerge ready':
                    self.assertTrue(owned and playing and not merged and not window, step)
                elif step == 'gpudeco live prepare':
                    self.assertTrue(paused and not owned, step)
                elif step.startswith('gpudeco rendermerge begin '):
                    self.assertTrue(owned and playing and not paused and not window, step)
                    window = True
                elif step == 'gpudeco rendermerge end':
                    self.assertTrue(window, step)
                    window = False
                elif step == 'gpudeco resources mark':
                    self.assertTrue(owned and paused, step)
                elif step in ('retry', 'waitend 40'):
                    self.assertTrue(owned and merged and playing and not paused, step)
                    owned = False
                elif step.startswith('gpudeco rendermerge pixel '):
                    self.assertTrue(owned and paused and not merged, step)


if __name__ == '__main__':
    unittest.main()
