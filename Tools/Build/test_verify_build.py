"""실제 오류 사례인 영상 누락·시간/색상·복귀 씬·빈 Addressables 처리를 검사합니다."""
import copy
from pathlib import Path
import tempfile
import unittest

from verify_build import CheckArtifact, CheckMetadata, StreamingPath, VideoPaths


class BuildChecks(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        for folder in ("Assets/2.Model/Prefabs/UI", "Assets/Data/Csv", "Assets/StreamingAssets/Video",
                       "Assets/AddressableAssetsData/AssetGroups", "ProjectSettings", "output/HumanGhost_Data/StreamingAssets/Video"):
            (self.root / folder).mkdir(parents=True)
        (self.root / "Assets/2.Model/Prefabs/UI/TitleScreen.prefab").write_text("  _videoFile: Video/Title.mp4\n", encoding="utf-8")
        (self.root / "Assets/Data/Csv/CutsceneStep.csv").write_text(
            "ID,MediaType,ResourceAddress\n1,Video,streaming:Video/Intro.mp4\n", encoding="utf-8")
        (self.root / "ProjectSettings/EditorBuildSettings.asset").write_text(
            "- enabled: 1\n  path: Assets/1.Scene/Title.unity\n", encoding="utf-8")
        self.output = self.root / "output"
        (self.output / "HumanGhost.exe").write_bytes(b"fixture")
        self.scenes = ["Assets/1.Scene/Title.unity", "Assets/1.Scene/MapDohwagolTest.unity"]
        self.WriteScenes(self.scenes)
        for name in ("Title.mp4", "Intro.mp4"):
            (self.root / "Assets/StreamingAssets/Video" / name).write_bytes(b"video fixture")
            (self.output / "HumanGhost_Data/StreamingAssets/Video" / name).write_bytes(b"video fixture")
        self.metadata = {"streams": [{"codec_type": "video", "codec_name": "h264", "pix_fmt": "yuv420p", "has_b_frames": 0,
                                     "color_space": "bt709", "color_transfer": "bt709", "color_primaries": "bt709"}]}
        self.frame = {"frames": [{"best_effort_timestamp_time": "0.000000"}]}

    def WriteScenes(self, scenes):
        (self.output / "BuildScenes.txt").write_text("\n".join(scenes), encoding="utf-8")

    def test_all_selected_start_scenes_preserve_return_and_new_game(self):
        for mode, first in (("build-settings", self.scenes[0]), ("dohwagol", self.scenes[1]),
                            ("preview", "Assets/1.Scene/GameplayPreview.unity")):
            with self.subTest(mode=mode):
                self.WriteScenes(list(dict.fromkeys([first] + self.scenes)))
                CheckArtifact(self.root, self.output, VideoPaths(self.root), mode)

    def test_missing_title_or_new_game_scene_fails(self):
        for scenes in ([self.scenes[0]], [self.scenes[1]]):
            self.WriteScenes(scenes)
            with self.assertRaisesRegex(ValueError, "필요한 씬"):
                CheckArtifact(self.root, self.output, VideoPaths(self.root), "build-settings")

    def test_wrong_first_scene_fails(self):
        self.WriteScenes(list(reversed(self.scenes)))
        with self.assertRaisesRegex(ValueError, "첫 씬"):
            CheckArtifact(self.root, self.output, VideoPaths(self.root), "build-settings")

    def test_missing_or_changed_built_intro_fails(self):
        built = self.output / "HumanGhost_Data/StreamingAssets/Video/Intro.mp4"
        built.write_bytes(b"different video")
        with self.assertRaisesRegex(ValueError, "원본과 다릅니다"):
            CheckArtifact(self.root, self.output, VideoPaths(self.root), "build-settings")
        built.unlink()
        with self.assertRaisesRegex(ValueError, "없거나 비어"):
            CheckArtifact(self.root, self.output, VideoPaths(self.root), "build-settings")

    def test_addressables_files_required_only_with_registered_assets(self):
        CheckArtifact(self.root, self.output, VideoPaths(self.root), "build-settings")
        (self.root / "Assets/AddressableAssetsData/AssetGroups/Group.asset").write_text(
            "    m_Address: ExampleSprite\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "Addressables"):
            CheckArtifact(self.root, self.output, VideoPaths(self.root), "build-settings")

    def test_start_guide_videos_checked_without_data_address(self):
        name = "StartGuide_Jump_A.mp4"
        (self.root / "Assets/StreamingAssets/Video" / name).write_bytes(b"guide fixture")
        self.assertIn("Video/" + name, [relative for relative, _ in VideoPaths(self.root)])
        with self.assertRaisesRegex(ValueError, "없거나 비어"):
            CheckArtifact(self.root, self.output, VideoPaths(self.root), "build-settings")
        (self.output / "HumanGhost_Data/StreamingAssets/Video" / name).write_bytes(b"guide fixture")
        CheckArtifact(self.root, self.output, VideoPaths(self.root), "build-settings")

    def test_addressable_video_rejected_before_linux_build(self):
        (self.root / "Assets/Data/Csv/CutsceneStep.csv").write_text(
            "ID,MediaType,ResourceAddress\n1,Video,Cutscene_Intro_Video\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "streaming:"):
            VideoPaths(self.root)

    def test_traversal_windows_path_and_empty_video_rejected(self):
        for relative in ("../outside.mp4", "/outside.mp4", "C:/video.mp4", "Video\\Intro.mp4"):
            with self.subTest(relative=relative), self.assertRaises(ValueError):
                StreamingPath(self.root / "Assets/StreamingAssets", relative)
        (self.root / "Assets/StreamingAssets/Video/Intro.mp4").write_bytes(b"")
        with self.assertRaisesRegex(ValueError, "비어"):
            VideoPaths(self.root)

    def test_color_codec_and_delayed_first_frame_rejected(self):
        CheckMetadata(self.metadata, self.frame, "fixture")
        for key, value in (("color_primaries", "unknown"), ("codec_name", "hevc"), ("pix_fmt", "yuv444p"), ("has_b_frames", 1)):
            metadata = copy.deepcopy(self.metadata)
            metadata["streams"][0][key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                CheckMetadata(metadata, self.frame, "fixture")
        with self.assertRaisesRegex(ValueError, "시작 시각"):
            CheckMetadata(self.metadata, {"frames": [{"best_effort_timestamp_time": "0.2"}]}, "fixture")


if __name__ == "__main__":
    unittest.main()
