import unittest
import numpy as np
from hd2d_field_textures import path_mask

class PathMaskTests(unittest.TestCase):
    def test_half_width_controls_real_metre_coverage(self):
        cells=[{'x':0,'y':0}]
        a=path_mask(cells,dict(halfWidth=.67,widthVariation=0,edgeVariation=0),181108,720,720)
        # Texel centres are .05m modulo .1: L1 .6 inside, .7 outside.
        self.assertEqual(255,a[360,365])
        self.assertEqual(0,a[360,366])
        b=path_mask(cells,dict(halfWidth=.8,widthVariation=0,edgeVariation=0),181108,720,720)
        self.assertEqual(255,b[360,366])
        self.assertGreater(np.count_nonzero(b),np.count_nonzero(a))

    def test_each_variation_control_changes_only_the_boundary(self):
        cells=[{'x':i,'y':0} for i in range(-20,21)]
        fixed=dict(halfWidth=.67,widthVariation=0,edgeVariation=0)
        a=path_mask(cells,fixed,181108,1440,1440)
        for key in ['widthVariation','edgeVariation']:
            settings=dict(fixed);settings[key]=.10
            b=path_mask(cells,settings,181108,1440,1440)
            self.assertFalse(np.array_equal(a,b),key)
            np.testing.assert_array_equal(b,path_mask(cells,settings,181108,1440,1440))
            # A .1m variation cannot alter the .5m core.
            core=path_mask(cells,dict(halfWidth=.5,widthVariation=0,edgeVariation=0),181108,1440,1440)>0
            self.assertTrue((b[core]==255).all())

if __name__=='__main__': unittest.main()
