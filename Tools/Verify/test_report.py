import copy
import json
from contextlib import redirect_stdout
import io
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import report as reader
import run as verify_run
from report import brief, select


class ReportTests(unittest.TestCase):
    def test_nested_failure_keeps_verdict_and_evidence(self):
        data={'status':'FAIL','results':{'unity':{'status':'FAIL','results':{
            'cleanup':{'status':'BLOCKED','error':'x'*10000},'compile':{'status':'PASS'}}}}}
        before=copy.deepcopy(data);summary=brief(data,'summary.json')
        self.assertEqual(data,before)
        self.assertEqual(summary['status'],'FAIL')
        self.assertEqual(summary['status_counts'],{'FAIL':1,'BLOCKED':1,'PASS':1})
        self.assertEqual(select(data,'unity.cleanup')['error'],'x'*10000)
        self.assertLess(len(json.dumps(summary)),1500)

    def test_truncated_failure_list_is_explicit(self):
        data={'status':'BLOCKED','results':{str(i):{'status':'NOT_RUN'} for i in range(50)}}
        summary=brief(data,'summary.json')
        self.assertEqual(summary['checks_omitted'],38)
        self.assertEqual(summary['problems_omitted'],47)
        self.assertEqual(summary['status_counts']['NOT_RUN'],50)

    def test_root_failure_without_children_is_visible(self):
        self.assertEqual(brief({'status':'FAIL','error':'failed'},'report')['error'],'failed')

    def test_expected_negative_control_does_not_rejudge_parent(self):
        data={'status':'PASS','results':{'original_contract':{'status':'FAIL','errors':['TRIANGLE_BUDGET']}}}
        self.assertEqual(brief(data,'report')['status'],'PASS')
        self.assertEqual(brief(data,'report')['status_counts']['FAIL'],1)

    def test_unknown_check_cannot_return_pass(self):
        with self.assertRaises(ValueError): select({'results':{}},'missing')

    def test_real_screen_list_shape_does_not_break_summary(self):
        data={'status':'PASS','results':{'unity':{'status':'PASS','results':{
            'screens':{'status':'PASS','checks':['PASS input 1280x720']*33}}}}}
        self.assertEqual(brief(data,'report')['checks']['unity.screens'],'PASS')
        self.assertEqual(len(select(data,'unity.screens')['checks']),33)

    def test_scope_not_run_is_not_hidden_by_parent_pass(self):
        data={'status':'PASS','visual_review_status':'NOT_RUN','results':{
            'map':{'status':'PASS','unity_status':'NOT_RUN'}}}
        summary=brief(data,'report')
        self.assertEqual(summary['status'],'PASS')
        self.assertEqual(summary['status_counts']['NOT_RUN'],2)
        self.assertEqual(select(data,'map.unity_status')['status'],'NOT_RUN')

    def test_run_compact_and_verbose_preserve_failed_evidence_and_exit(self):
        with tempfile.TemporaryDirectory() as temp:
            with patch.object(verify_run,'ROOT',Path(temp)), patch.object(verify_run,'fingerprint',return_value={}), \
                 patch.object(verify_run,'documents',return_value={'status':'FAIL','errors':['broken link']}), \
                 patch.object(verify_run,'data_check',return_value={'status':'PASS'}), \
                 patch.object(verify_run,'map_oracle_foundation',return_value={'status':'PASS'}):
                reports=[]
                for flags in ([],['--verbose']):
                    stream=io.StringIO()
                    with patch('sys.argv',['run.py','local',*flags]),redirect_stdout(stream):
                        self.assertEqual(verify_run.main(),1)
                    response=json.loads(stream.getvalue())
                    self.assertEqual(response['status'],'FAIL')
                    reports.append(json.loads(Path(response['report']).read_text())['results'])
                self.assertEqual(reports[0],reports[1])
                self.assertEqual(reports[0]['docs']['errors'],['broken link'])

    def test_detail_reader_keeps_blocked_exit_and_truncation(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);out=root/'output';out.mkdir();file=out/'report.json'
            file.write_text(json.dumps({'status':'FAIL','results':{'unity':{
                'status':'BLOCKED','error':'failure '*1000}}}))
            stream=io.StringIO()
            with patch.object(reader,'ROOT',root),patch('sys.argv',['report.py',str(file),'--check','unity']),redirect_stdout(stream):
                self.assertEqual(reader.main(),2)
            response=json.loads(stream.getvalue())
            self.assertGreater(response['characters_omitted'],0)
            self.assertEqual(response['status'],'BLOCKED')


if __name__ == '__main__': unittest.main()
