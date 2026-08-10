import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from prompt import SYSTEM_PROMPT, build_answer_input, build_rewrite_input


class PromptTest(unittest.TestCase):
    def test_prompt_is_chatbot_only_and_document_grounded(self):
        lowered = SYSTEM_PROMPT.lower()

        self.assertIn("chatbot", lowered)
        self.assertIn("chỉ trả lời", lowered)
        self.assertIn("trích dẫn", lowered)
        self.assertNotIn("voicebot", lowered)
        self.assertNotIn("podcast", lowered)
        self.assertNotIn("mln111", lowered)

    def test_answer_input_separates_history_sources_and_question(self):
        value = build_answer_input(
            "Nó có ưu điểm gì?",
            [{"role": "user", "content": "Dependency Injection là gì?"}],
            [("S1", "Nội dung tài liệu")],
        )

        self.assertIn("[HISTORY]", value)
        self.assertIn("[SOURCES]", value)
        self.assertIn("[S1]", value)
        self.assertIn("[QUESTION]", value)

    def test_rewrite_input_contains_recent_context(self):
        value = build_rewrite_input(
            "Nó hoạt động thế nào?",
            [{"role": "assistant", "content": "MVC gồm ba thành phần."}],
        )

        self.assertIn("MVC gồm ba thành phần.", value)
        self.assertIn("Nó hoạt động thế nào?", value)


if __name__ == "__main__":
    unittest.main()
