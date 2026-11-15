"""공급 영상에서 Windows 배포용 사본을 만듭니다. 원본과 출력은 별도 경로로 지정합니다."""
import argparse
from pathlib import Path
import subprocess
from verify_build import CheckVideo


def Main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--ffmpeg", default="ffmpeg")
    parser.add_argument("--ffprobe")
    args = parser.parse_args()
    if args.source.resolve() == args.output.resolve() or args.output.exists():
        parser.error("원본을 보존하도록 존재하지 않는 별도 출력 파일을 지정해 주세요.")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    # 프레임 순서 재배치를 없애 Windows Media Foundation의 첫 프레임 지연 경고를 방지합니다.
    # 공급 영상의 해상도·프레임률은 그대로 사용하고 HD 영상의 색상 규격을 명시합니다.
    command = [args.ffmpeg, "-hide_banner", "-v", "error", "-nostdin", "-n", "-i", str(args.source),
               "-map", "0:v:0", "-map", "0:a?", "-vf", "setpts=PTS-STARTPTS", "-c:v", "libx264",
               "-preset", "slow", "-crf", "18", "-bf", "0", "-pix_fmt", "yuv420p",
               "-color_primaries", "bt709", "-color_trc", "bt709", "-colorspace", "bt709",
               "-color_range", "tv", "-af", "asetpts=PTS-STARTPTS", "-c:a", "aac", "-b:a", "192k",
               "-bsf:v", "h264_metadata=colour_primaries=1:transfer_characteristics=1:matrix_coefficients=1",
               "-movflags", "+faststart", str(args.output)]
    subprocess.run(command, check=True)
    suffix = Path(args.ffmpeg).suffix
    ffprobe = args.ffprobe or str(Path(args.ffmpeg).with_name("ffprobe" + suffix))
    CheckVideo(args.output, ffprobe, args.ffmpeg)
    print("배포용 영상 생성: " + str(args.output))


if __name__ == "__main__":
    Main()
