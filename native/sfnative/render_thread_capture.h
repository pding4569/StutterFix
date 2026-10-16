/* StutterFix original code, MIT. No D3D state or Unity-owned buffer access.
 * The event captures an owned kernel handle, not a managed callback/pointer.
 * A generation cancels queued events across disable/reload. The consumer owns
 * a successfully taken handle and must CloseHandle it after restoring changes. */
static SRWLOCK sf_rt_capture_lock = SRWLOCK_INIT;
static int sf_rt_capture_generation = 0;
static int sf_rt_capture_pending = 0;
static HANDLE sf_rt_capture_handle = NULL;

SF_API int sf_render_thread_capture_begin_v1(void)
{
    int generation = 0;
    AcquireSRWLockExclusive(&sf_rt_capture_lock);
    if (sf_rt_capture_handle) CloseHandle(sf_rt_capture_handle);
    sf_rt_capture_handle = NULL;
    sf_rt_capture_pending = 0;
    /* Do not wrap to an old queued event ID. */
    if (sf_rt_capture_generation < INT_MAX) {
        generation = ++sf_rt_capture_generation;
        sf_rt_capture_pending = 1;
    }
    ReleaseSRWLockExclusive(&sf_rt_capture_lock);
    return generation;
}

static void __stdcall sf_render_thread_capture_event(int generation)
{
    AcquireSRWLockExclusive(&sf_rt_capture_lock);
    if (generation > 0 && generation == sf_rt_capture_generation && sf_rt_capture_pending) {
        HANDLE handle = NULL;
        if (DuplicateHandle(GetCurrentProcess(), GetCurrentThread(), GetCurrentProcess(),
            &handle, THREAD_QUERY_INFORMATION | THREAD_SET_INFORMATION | SYNCHRONIZE, FALSE, 0))
            sf_rt_capture_handle = handle;
        sf_rt_capture_pending = 0;
    }
    ReleaseSRWLockExclusive(&sf_rt_capture_lock);
}

SF_API void *sf_render_thread_capture_event_ptr_v1(void)
{ return (void *)sf_render_thread_capture_event; }

SF_API HANDLE sf_render_thread_capture_take_v1(int generation)
{
    HANDLE handle = NULL;
    AcquireSRWLockExclusive(&sf_rt_capture_lock);
    if (generation > 0 && generation == sf_rt_capture_generation) {
        handle = sf_rt_capture_handle;
        sf_rt_capture_handle = NULL;
    }
    ReleaseSRWLockExclusive(&sf_rt_capture_lock);
    return handle;
}

SF_API void sf_render_thread_capture_cancel_v1(int generation)
{
    AcquireSRWLockExclusive(&sf_rt_capture_lock);
    if (generation > 0 && generation == sf_rt_capture_generation) {
        sf_rt_capture_pending = 0;
        if (sf_rt_capture_handle) CloseHandle(sf_rt_capture_handle);
        sf_rt_capture_handle = NULL;
    }
    ReleaseSRWLockExclusive(&sf_rt_capture_lock);
}
