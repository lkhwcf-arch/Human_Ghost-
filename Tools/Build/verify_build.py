"""Linux 빌드 입력 영상과 Windows 결과물의 연결·디코딩을 검사합니다."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import re
import subprocess


def StreamingPath(root, relative):
    # Windows용 경로를 Linux에서도 같은 규칙으로 검사합니다.
    if not relative or "\\" in relative or ":" in relative:
        raise ValueError("영상 주소는 슬래시를 사용하는 상대 경로여야 합니다: " + relative)
    path = (root / relative).resolve()
    if not path.is_relative_to(root.resolve()) or path.suffix.lower() != ".mp4":
        raise ValueError("영상은 StreamingAssets 내부 MP4여야 합니다: " + relative)
    if not path.is_file() or path.stat().st_size == 0:
        raise ValueError("영상 파일이 없거나 비어 있습니다: " + str(path))
    return path


def VideoPaths(project):
    prefab = (project / "Assets/2.Model/Prefabs/UI/TitleScreen.prefab").read_text(encoding="utf-8")
    match = re.search(r"^  _videoFile: (.+)$", prefab, re.MULTILINE)
    if not match:
        raise ValueError("타이틀 영상 상대 경로가 없습니다.")
    paths = {match.group(1).strip()}
    with (project / "Assets/Data/Csv/CutsceneStep.csv").open(encoding="utf-8-sig", newline="") as source:
        for row in csv.DictReader(source):
            if row["MediaType"] != "Video":
                continue
            address = row["ResourceAddress"]
            if not address.startswith("streaming:"):
                raise ValueError("Linux 빌드의 컷씬 영상은 streaming: 파일 주소를 사용해야 합니다. 행: " + row["ID"])
            paths.add(address[len("streaming:"):])
    root = project / "Assets/StreamingAssets"
    # 시작 맵 안내 영상은 데이터 주소 없이 이름 규칙(StartGuide_동작[_A|_B])으로 직접 재생하므로 모두 검사합니다.
    paths.update("Video/" + file.name for file in sorted((root / "Video").glob("StartGuide_*.mp4")))
    paths.update(StartGuideVideos(project, root))
    return [(relative, StreamingPath(root, relative)) for relative in sorted(paths)]


def StartGuideVideos(project, root):
    # StartMapControlsView.ResolveVideo와 같은 규칙입니다. A(방향키)=_A, B(WASD)=_B, 없으면 접미사 없는 파일.
    # Linux 빌드는 대소문자를 구분하므로 UI_Lang Key의 동작 이름과 파일 이름이 정확히 같아야 합니다.
    table = project / "Assets/Data/Csv/UI_Lang.csv"
    if not table.is_file():
        return set()
    names = {file.name for file in (root / "Video").glob("StartGuide_*.mp4")}
    required = set()
    with table.open(encoding="utf-8-sig", newline="") as source:
        for row in csv.DictReader(source):
            key = row["Key"]
            if not key.startswith("StartGuide."):
                continue
            action = key[len("StartGuide."):]
            for suffix, layout in (("_A", "A 방향키"), ("_B", "B WASD")):
                name = next((candidate for candidate in ("StartGuide_" + action + suffix + ".mp4", "StartGuide_" + action + ".mp4")
                             if candidate in names), None)
                if name is None:
                    raise ValueError("시작 안내 영상이 없습니다(" + layout + "): Video/StartGuide_" + action + suffix +
                                     ".mp4 또는 StartGuide_" + action + ".mp4. UI_Lang 행: " + row["ID"])
                required.add("Video/" + name)
    return required


def Run(command):
    result = subprocess.run(command, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=600)
    if result.returncode:
        raise ValueError("영상 도구 실행 실패: " + str(command[0]) + "\n" + result.stderr[-4000:])
    return result.stdout


def CheckMetadata(metadata, first_frame, path):
    streams = metadata.get("streams", [])
    videos = [stream for stream in streams if stream.get("codec_type") == "video"]
    if len(videos) != 1:
        raise ValueError("영상 트랙은 하나여야 합니다: " + str(path))
    video = videos[0]
    if video.get("codec_name") != "h264" or video.get("pix_fmt") != "yuv420p":
        raise ValueError("Windows 재생용 H.264/yuv420p 영상이 필요합니다: " + str(path))
    if video.get("has_b_frames") != 0:
        raise ValueError("Windows 첫 프레임 지연을 피하도록 B 프레임 없이 인코딩해 주세요: " + str(path))
    if any(video.get(key) != "bt709" for key in ("color_space", "color_transfer", "color_primaries")):
        raise ValueError("영상 색상 정보 BT.709를 명시해 주세요: " + str(path))
    frames = first_frame.get("frames", [])
    if not frames or "best_effort_timestamp_time" not in frames[0]:
        raise ValueError("첫 영상 프레임을 읽지 못했습니다: " + str(path))
    if abs(float(frames[0]["best_effort_timestamp_time"])) > 0.001:
        raise ValueError("첫 영상 프레임의 시작 시각은 0이어야 합니다: " + str(path))
    for stream in streams:
        if stream.get("codec_type") == "audio" and stream.get("codec_name") != "aac":
            raise ValueError("Windows 재생용 AAC 오디오가 필요합니다: " + str(path))


def CheckVideo(path, ffprobe, ffmpeg):
    metadata = json.loads(Run([ffprobe, "-v", "error", "-show_streams", "-of", "json", str(path)]))
    first_frame = json.loads(Run([ffprobe, "-v", "error", "-select_streams", "v:0", "-read_intervals", "%+#1",
                                 "-show_frames", "-show_entries", "frame=best_effort_timestamp_time", "-of", "json", str(path)]))
    CheckMetadata(metadata, first_frame, path)
    # 파일 존재·해시 검사로 잡히지 않는 중간 프레임/오디오 손상도 끝까지 읽습니다.
    Run([ffmpeg, "-v", "error", "-xerror", "-err_detect", "explode", "-i", str(path),
         "-map", "0:v:0", "-map", "0:a?", "-f", "null", "-"])


def FileDigest(path):
    # 로컬 Python 3.10과 CI에서 같은 해시 검사를 사용하고 영상 전체를 메모리에 올리지 않습니다.
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.digest()


def CheckArtifact(project, output, videos, scene):
    if not (output / "HumanGhost.exe").is_file():
        raise ValueError("빌드 결과에 HumanGhost.exe가 없습니다.")
    scenes = (output / "BuildScenes.txt").read_text(encoding="utf-8-sig").splitlines()
    required = {"Assets/1.Scene/Title.unity", "Assets/1.Scene/MapDohwagolTest.unity"}
    if not required.issubset(scenes):
        raise ValueError("타이틀 복귀·새 게임에 필요한 씬이 빌드 목록에 없습니다.")
    if scene == "build-settings":
        settings = (project / "ProjectSettings/EditorBuildSettings.asset").read_text(encoding="utf-8")
        expected = re.findall(r"- enabled: 1\s+path: (.+)", settings)[0].strip()
    else:
        expected = "Assets/1.Scene/" + {"dohwagol": "MapDohwagolTest", "preview": "GameplayPreview"}[scene] + ".unity"
    if not scenes or scenes[0] != expected:
        raise ValueError("선택한 시작 씬과 빌드의 첫 씬이 다릅니다.")
    for relative, source in videos:
        built = StreamingPath(output / "HumanGhost_Data/StreamingAssets", relative)
        if FileDigest(source) != FileDigest(built):
            raise ValueError("빌드 영상이 원본과 다릅니다: " + relative)
    # 등록 리소스가 없는 프로젝트에는 빈 Addressables 번들을 강제하지 않습니다.
    groups = project / "Assets/AddressableAssetsData/AssetGroups"
    has_addresses = any(re.search(r"^\s+m_Address:", file.read_text(encoding="utf-8"), re.MULTILINE) for file in groups.glob("*.asset"))
    if has_addresses:
        root = output / "HumanGhost_Data/StreamingAssets/aa"
        if not (root / "settings.json").is_file() or not any(root.rglob("catalog*")) or not any(root.rglob("*.bundle")):
            raise ValueError("등록된 Addressables의 런타임 설정·카탈로그·번들이 없습니다.")


def Main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--build-dir", type=Path)
    parser.add_argument("--scene", choices=["build-settings", "dohwagol", "preview"], default="build-settings")
    parser.add_argument("--ffprobe", default="ffprobe")
    parser.add_argument("--ffmpeg", default="ffmpeg")
    args = parser.parse_args()
    try:
        videos = VideoPaths(args.project)
        for relative, path in videos:
            CheckVideo(path, args.ffprobe, args.ffmpeg)
            print("영상 전체 디코딩·시작 시각·색상 정보 통과: " + relative)
        if args.build_dir:
            CheckArtifact(args.project, args.build_dir, videos, args.scene)
            print("시작 씬·타이틀 복귀·새 게임 씬과 결과물 영상 일치 확인을 통과했습니다.")
    except (ValueError, OSError, subprocess.TimeoutExpired) as error:
        parser.exit(1, str(error) + "\n")


if __name__ == "__main__":
    Main()
