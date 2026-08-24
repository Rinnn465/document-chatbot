import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from prompt import (
    GROUNDED_RETRY_INSTRUCTION,
    REWRITE_PROMPT,
    SYSTEM_PROMPT,
    build_answer_input,
    build_rewrite_input,
)


class PromptTest(unittest.TestCase):
    def test_prompt_is_chatbot_only_and_document_grounded(self):
        lowered = SYSTEM_PROMPT.lower()

        self.assertIn("chatbot", lowered)
        self.assertIn("chỉ trả lời", lowered)
        self.assertIn("trích dẫn", lowered)
        self.assertIn("thứ tự từ trên xuống", lowered)
        self.assertNotIn("voicebot", lowered)
        self.assertNotIn("podcast", lowered)
        self.assertNotIn("mln111", lowered)
        self.assertIn("cùng ngôn ngữ với nội dung trong sources", lowered)
        self.assertIn("question bằng tiếng việt nhưng sources bằng tiếng anh", lowered)
        self.assertIn("không dịch nội dung sang tiếng việt", lowered)
        self.assertIn("không được mở đầu", lowered)
        self.assertIn("**in đậm**", lowered)

    def test_grounded_retry_keeps_the_cited_source_language(self):
        lowered = GROUNDED_RETRY_INSTRUCTION.lower()

        self.assertIn("language used by the cited sources", lowered)
        self.assertIn("regardless of the question language", lowered)
        self.assertIn("do not translate", lowered)

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

    def test_rewrite_prompt_targets_english_course_documents(self):
        lowered = REWRITE_PROMPT.lower()

        self.assertIn("english search queries", lowered)
        self.assertIn("translate the question", lowered)
        self.assertIn("comparison", lowered)


if __name__ == "__main__":
    unittest.main()
