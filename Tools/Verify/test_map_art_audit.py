import unittest
from fractions import Fraction as F
from map_art_audit import contained_edge
class ContainmentTests(unittest.TestCase):
    def setUp(self):self.poly=tuple((F(x),F(y)) for x,y in [(0,0),(6,0),(6,2),(2,2),(2,6),(0,6)])
    def test_inside_edge(self):self.assertTrue(contained_edge((F(1),F(1)),(F(5),F(1)),self.poly))
    def test_concave_bridge_endpoints_inside(self):self.assertFalse(contained_edge((F(1),F(5)),(F(5),F(1)),self.poly))
    def test_boundary_edge_closed_containment(self):self.assertTrue(contained_edge((F(0),F(0)),(F(6),F(0)),self.poly))
    def test_tiny_notch_crossing(self):self.assertFalse(contained_edge((F(2)-F(1,10**12),F(3)),(F(2)+F(1,10**12),F(3)),self.poly))
    def test_stationary_outside(self):self.assertFalse(contained_edge((F(3),F(3)),(F(3),F(3)),self.poly))
if __name__=='__main__':unittest.main()
