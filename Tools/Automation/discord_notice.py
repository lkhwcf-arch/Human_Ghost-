"""코드 반영과 빌드 결과를 구분해 공지문을 만듭니다."""
import json
import os
import sys
from discord_webhook import Send


def BuildNotice(env):
    mode = env.get("BUILD_MODE") or "development"
    modeNames = {"development": "테스트 빌드", "release": "정식 빌드"}
    if mode not in modeNames:
        raise ValueError("빌드 종류는 development 또는 release로 지정해주세요.")
    test = env.get("NOTICE_OPERATION") == "notice-test"
    checked = env.get("CHECK_RESULT", "skipped")
    built = env.get("BUILD_RESULT", "skipped")
    artifact = env.get("BUILD_ARTIFACT_URL", "")
    if test:
        title, description, color = "빌드 채널 연결 테스트", "빌드 전용 채널 연결을 확인했습니다. 게임 빌드는 실행하지 않았습니다.", 5025616
    elif checked == "success" and built == "success" and artifact:
        title, description, color = "Windows 빌드 완료", f"게임 파일을 내려받을 수 있습니다.\n[게임 파일 다운로드]({artifact})\n압축을 풀고 HumanGhost.exe를 실행하세요.", 5025616
    elif checked == "cancelled" or built == "cancelled":
        title, description, color = "빌드 작업 취소", "작업이 취소됐습니다. 완성된 새 게임 파일을 안내하지 않습니다.", 16763904
    elif checked != "success":
        title, description, color = "빌드 전 검사 실패", "데이터·게임 코어 검사 또는 검사 환경 준비에 실패해 빌드를 진행하지 않았습니다.", 15158332
    else:
        title, description, color = "빌드·결과물 보관 실패", "Unity 인증·빌드 또는 결과물 보관에 실패했습니다. 실행 기록의 실패 단계를 확인해주세요.", 15158332
    return {
        "title": f"[클라이언트] {title}", "description": description, "color": color,
        "url": f"{env['GITHUB_SERVER_URL']}/{env['GITHUB_REPOSITORY']}/actions/runs/{env['GITHUB_RUN_ID']}",
        "fields": [
            {"name": "브랜치", "value": env["GITHUB_REF_NAME"][:1024], "inline": True},
            {"name": "커밋", "value": env["GITHUB_SHA"][:7], "inline": True},
            {"name": "빌드 번호", "value": env["GITHUB_RUN_NUMBER"], "inline": True},
            {"name": "빌드 종류", "value": modeNames[mode], "inline": True},
            {"name": "시작 장면", "value": env.get("BUILD_SCENE", "preview"), "inline": True},
        ],
        "footer": {"text": env["GITHUB_REPOSITORY"][:2048]},
    }


def MergeNotice(env, event):
    if env["GITHUB_EVENT_NAME"] == "workflow_dispatch":
        title = "[클라이언트] 알림 연결 테스트"
        description = "개발 채널 연결이 완료됐습니다. 지정 브랜치에 푸시되면 공지합니다."
        url = f"{env['GITHUB_SERVER_URL']}/{env['GITHUB_REPOSITORY']}/actions/runs/{env['GITHUB_RUN_ID']}"
        fields = []
    else:
        if event.get("deleted"):
            return None
        branch = event["ref"].removeprefix("refs/heads/")
        commits = event.get("commits", [])
        title = f"[클라이언트] {branch} 반영 완료"
        lines = [f"- {commit['id'][:7]} {commit['message'].splitlines()[0][:200]}" for commit in commits[:10]]
        if len(commits) > 10:
            lines.append(f"외 {len(commits) - 10}건은 변경 내역 링크에서 확인해주세요.")
        description = "\n".join(lines) or "브랜치 변경 내역은 링크에서 확인해주세요."
        url = event.get("compare") or f"{env['GITHUB_SERVER_URL']}/{env['GITHUB_REPOSITORY']}/commit/{event['after']}"
        fields = [
            {"name": "반영 브랜치", "value": branch[:1024], "inline": True},
            {"name": "반영 담당자", "value": env["GITHUB_ACTOR"][:1024], "inline": True},
        ]
    return {"title": title[:256], "description": description[:3000], "url": url, "color": 5025616,
            "fields": fields, "footer": {"text": env["GITHUB_REPOSITORY"][:2048]}}


def Main():
    mode = sys.argv[1] if len(sys.argv) == 2 else ""
    if mode == "build":
        embed = BuildNotice(os.environ)
        webhook = os.environ.get("DISCORD_BUILD_WEBHOOK_URL", "")
    elif mode == "merge":
        with open(os.environ["GITHUB_EVENT_PATH"], encoding="utf-8") as source:
            embed = MergeNotice(os.environ, json.load(source))
        webhook = os.environ.get("DISCORD_WEBHOOK_URL", "")
    else:
        raise ValueError("공지 종류는 merge 또는 build로 지정해주세요.")
    if embed is not None:
        Send(webhook, embed)
        print("디스코드 공지를 전송했습니다.")


if __name__ == "__main__":
    try:
        Main()
    except (ValueError, RuntimeError, OSError, KeyError) as error:
        # 웹훅 주소나 라이선스 원문을 출력하지 않습니다.
        sys.exit(f"공지 처리 실패: {error}")
