"""웹훅을 호출하지 않고 빌드 종류와 성공·실패 공지의 계약을 확인합니다."""
import unittest
from discord_notice import BuildNotice


class BuildNoticeChecks(unittest.TestCase):
    def CreateEnvironment(self, **changes):
        env = {
            "GITHUB_SERVER_URL": "https://github.com",
            "GITHUB_REPOSITORY": "team/game",
            "GITHUB_RUN_ID": "100",
            "GITHUB_REF_NAME": "Develop",
            "GITHUB_SHA": "1234567890abcdef",
            "GITHUB_RUN_NUMBER": "12",
            "NOTICE_OPERATION": "build",
            "BUILD_SCENE": "build-settings",
            "CHECK_RESULT": "success",
            "BUILD_RESULT": "success",
            "BUILD_ARTIFACT_URL": "https://github.com/team/game/actions/artifacts/100",
        }
        env.update(changes)
        return env

    def test_success_keeps_mode_and_download(self):
        for mode, label in (("development", "테스트 빌드"), ("release", "정식 빌드")):
            with self.subTest(mode=mode):
                env = self.CreateEnvironment(BUILD_MODE=mode)
                notice = BuildNotice(env)
                self.assertIn(env["BUILD_ARTIFACT_URL"], notice["description"])
                self.assertIn({"name": "빌드 종류", "value": label, "inline": True}, notice["fields"])

    def test_failure_never_offers_download(self):
        for mode in ("development", "release"):
            for checked, built in (("failure", "skipped"), ("success", "failure"), ("success", "cancelled")):
                with self.subTest(mode=mode, checked=checked, built=built):
                    env = self.CreateEnvironment(BUILD_MODE=mode, CHECK_RESULT=checked, BUILD_RESULT=built)
                    self.assertNotIn(env["BUILD_ARTIFACT_URL"], BuildNotice(env)["description"])

    def test_missing_artifact_is_failure(self):
        notice = BuildNotice(self.CreateEnvironment(BUILD_MODE="release", BUILD_ARTIFACT_URL=""))
        self.assertIn("실패", notice["title"])

    def test_notice_test_does_not_claim_game_was_built(self):
        notice = BuildNotice(self.CreateEnvironment(BUILD_MODE="release", NOTICE_OPERATION="notice-test"))
        self.assertIn("게임 빌드는 실행하지 않았습니다", notice["description"])
        self.assertNotIn("게임 파일 다운로드", notice["description"])

    def test_older_environment_defaults_to_development(self):
        notice = BuildNotice(self.CreateEnvironment())
        self.assertIn({"name": "빌드 종류", "value": "테스트 빌드", "inline": True}, notice["fields"])

    def test_unknown_mode_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "빌드 종류"):
            BuildNotice(self.CreateEnvironment(BUILD_MODE="unknown"))


if __name__ == "__main__":
    unittest.main()
