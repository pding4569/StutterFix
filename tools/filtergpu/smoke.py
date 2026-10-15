import sys
sys.path.insert(0,'tools/sfmeasure')
import server as s
print(s.sf_run(['set FrameGenOutside 0','set Language ko','ui 1.2','wait 2','shot filters-ko','set Language en','wait 2','shot filters-en','ui close','filtergpu on','wait 3','filtergpu state','filtergpu off','quit'],timeout_min=3,tag='filters-smoke')[0])


