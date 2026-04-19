import sys
import tempfile
import unittest
import uuid
import warnings
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO_ROOT / "worker"))


class AdmissionErrorAndIdempotencyTests(unittest.TestCase):
    def setUp(self):
        warnings.filterwarnings(
            "ignore",
            message="The ``declarative_base\\(\\)`` function is now available",
            category=DeprecationWarning,
        )

    def test_failure_values_populate_reason_stage_and_timestamp(self):
        import FileWatcher as watcher_module

        with tempfile.TemporaryDirectory() as temp_dir:
            watcher = watcher_module.FileWatcher(Path(temp_dir))

            values = watcher._failure_values(
                status="Rejected",
                failure_code="MIME_MISMATCH",
                failure_stage="ContentTypeValidation",
                failure_message=None,
            )

        self.assertEqual(values["Status"], "Rejected")
        self.assertEqual(values["FailureCode"], "MIME_MISMATCH")
        self.assertEqual(values["FailureStage"], "ContentTypeValidation")
        self.assertIn("does not match", values["FailureMessage"])
        self.assertIsNotNone(values["LastAttemptAt"])

    def test_upload_claim_uses_expected_state_guard(self):
        import FileWatcher as watcher_module

        class FakeResult:
            rowcount = 1

        class FakeDb:
            def __init__(self):
                self.statement = None

            def execute(self, statement):
                self.statement = statement
                return FakeResult()

        upload_id = uuid.uuid4()
        fake_db = FakeDb()

        with tempfile.TemporaryDirectory() as temp_dir:
            watcher = watcher_module.FileWatcher(Path(temp_dir))
            claimed = watcher._transition_upload_status(
                fake_db,
                upload_id,
                expected_statuses={"Pending"},
                new_status="Stabilizing",
            )

        compiled = str(fake_db.statement.compile(compile_kwargs={"literal_binds": True}))

        self.assertTrue(claimed)
        self.assertIn('"Status" IN (\'Pending\')', compiled)
        self.assertIn("Stabilizing", compiled)

    def test_ingestion_model_declares_binary_hash_unique_guard(self):
        import models as worker_models

        constraints = {
            constraint.name
            for constraint in worker_models.FileIngestion.__table__.constraints
        }

        self.assertIn("UQ_FileIngestions_BinaryHash", constraints)


if __name__ == "__main__":
    unittest.main()
