import asyncio
import os
import sys
import tempfile
import types
import unittest
import warnings
from pathlib import Path
from unittest.mock import patch


REPO_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO_ROOT))
sys.path.insert(0, str(REPO_ROOT / "worker"))


class FakeUploadFile:
    def __init__(self, filename, content):
        self.filename = filename
        self._content = content

    async def read(self):
        return self._content


class FakeDb:
    def __init__(self):
        self.events = []
        self.upload = None

    def add(self, upload):
        self.events.append("add")
        self.upload = upload

    def commit(self):
        self.events.append("commit")

    def refresh(self, upload):
        self.events.append("refresh")

    def rollback(self):
        self.events.append("rollback")


class FileStabilityTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        asyncio.get_running_loop().slow_callback_duration = 5
        warnings.filterwarnings(
            "ignore",
            message="The ``declarative_base\\(\\)`` function is now available",
            category=DeprecationWarning,
        )

    async def test_save_upload_commits_upload_record_before_final_rename(self):
        from backend.src.services.file_service import FileService

        fake_db = FakeDb()

        with tempfile.TemporaryDirectory() as temp_dir:
            service = FileService()
            service.upload_dir = Path(temp_dir)

            def assert_committed_before_rename(src, dst):
                self.assertIn("commit", fake_db.events)
                os.replace(src, dst)

            with patch("os.rename", side_effect=assert_committed_before_rename):
                upload = await service.save_upload(
                    FakeUploadFile("sample.txt", b"hello"),
                    fake_db,
                    "user@example.com",
                )

        self.assertEqual(upload.Status, "Pending")
        self.assertEqual(fake_db.events[:2], ["add", "commit"])
        self.assertIn("refresh", fake_db.events)

    async def test_wait_for_stable_file_rejects_changing_size(self):
        import FileWatcher as watcher_module

        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "sample__11111111-1111-1111-1111-111111111111.txt"
            path.write_text("a", encoding="utf-8")
            watcher = watcher_module.FileWatcher(Path(temp_dir))

            async def grow_file():
                await asyncio.sleep(0.015)
                path.write_text("abcdef", encoding="utf-8")
                await asyncio.sleep(0.015)
                path.write_text("abcdefghijk", encoding="utf-8")

            grow_task = asyncio.create_task(grow_file())
            try:
                is_stable = await watcher.wait_for_stable_file(path, checks=3, interval_seconds=0.02)
            finally:
                await grow_task

        self.assertFalse(is_stable)

    async def test_process_file_defers_when_upload_row_is_not_visible_yet(self):
        import FileWatcher as watcher_module

        class FakeQuery:
            def filter(self, *args, **kwargs):
                return self

            def first(self):
                return None

        class FakeDbSession:
            def __init__(self):
                self.executes = 0
                self.commits = 0
                self.closed = False

            def query(self, *args, **kwargs):
                return FakeQuery()

            def execute(self, *args, **kwargs):
                self.executes += 1
                return types.SimpleNamespace(rowcount=0)

            def commit(self):
                self.commits += 1

            def rollback(self):
                pass

            def close(self):
                self.closed = True

        fake_db = FakeDbSession()

        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "sample__11111111-1111-1111-1111-111111111111.txt"
            path.write_text("hello", encoding="utf-8")
            watcher = watcher_module.FileWatcher(Path(temp_dir))

            with patch.object(watcher_module, "SessionLocal", return_value=fake_db):
                with patch.object(watcher, "wait_for_stable_file", return_value=True):
                    await watcher.process_file(str(path))

        self.assertEqual(fake_db.commits, 0)
        self.assertEqual(fake_db.executes, 0)
        self.assertTrue(fake_db.closed)


if __name__ == "__main__":
    unittest.main()
