# HAP 3D 작업 안내

## 작업 시작

README.md, Docs/이선동_HAP3D_기획서_원문.txt, Docs/작업목록.txt를 읽고 현재 상태를 확인한다. 이 프로젝트는 이선동의 학교 2차 프로젝트용 HAP 3D다. 과거에 잘못 제안했던 반송 게임 규칙은 가져오지 않는다.

## 승인된 방향

- 빛블록의 음양 규칙 계승: 양 단독이면 길 유지, 음 단독 접근이면 붕괴, 양과 음이 함께면 안정된 길. 단순한 색별 통과 규칙으로 바꾸지 않고, 세이브포인트나 빛 중계 장치와 동일하게 취급하지 않는다.
- PC 1인 플레이와 WASD 이동·Tab 캐릭터 전환 조작 승인됨. 양·음 캐릭터를 번갈아 조작한다. 네트워크 멀티와 로컬 2인 동시 조작은 범위에 넣지 않는다.
- 작은 복셀 블록섬, 흑백 캐릭터, 청록 빛길, 고정 사선 직교 시점이 승인된 비주얼 방향이다. 카메라의 구체 위치·각도와 판정 영역은 실험값이다.
- 세 퍼즐, 전체 플레이 3~5분 목표. 모바일은 비주얼 참고이며 플랫폼 대상으로 확정되지 않았다. 행성 공간은 후속 확장이다.
- 1단계 ‘함께 건너기’: 함께 발판을 건너 바닥의 공동 목표에 도착.
- 2단계 ‘다른 바닥’: 영구 바닥에서 천장으로 중력을 반전한 뒤 옆길로 이동.
- 3단계 ‘뒤집혀도 함께’: 아래 빛다리 → 안전 발판 → 중력 반전 → 천장 빛다리 → 공동 목표.

## 아직 실험 중인 조건

게임은 Stage1에서 바로 시작한다. 화면 왼쪽 위에는 Stage1 / Stage2 / Stage3 표시만 나오며, Stage1에서는 WASD/Tab/G/R 키 그림도 보인다. Mac 실제 입력으로 키캡 반응, WASD·Tab·G·R을 모두 누른 뒤 숨김, Stage1→2→3 자동 이동, 마지막 두 목표 완료 후 R 재시작, Esc 종료를 확인했다. Esc는 종료키라 학습 대상에서 제외한다. 새 Nunito 글꼴·화면·카메라 13단계 검증도 통과했다. 설명·선택 HUD·제목·완료 패널은 없다. 중간 단계 목표 도착 뒤 0.9초 후 자동으로 다음 단계로 넘어간다. 마지막 목표 도착 뒤 R, Enter 또는 Space로 처음부터 시작한다. R은 진행 중 현재 단계 재시작, Esc는 종료다.

음 단독 붕괴 0.6초, 붕괴 후 복구 2초, 접근·함께 판정 범위, 안정화 우선순위, 접지 중 개별 중력 반전 방식, 카메라의 세부값은 구현 실험값이지 확정 규칙이 아니다. 점프·공중 조작, 행성 중력도 확정하지 않는다. v0.1.0에서는 복셀 3개 퍼즐 구현을 완료했고, 13단계 그래픽 자동 검증이 `HAP3D_VALIDATION_OK`로 통과했다. 세 단계 모두 제작된 시작 위치에서 검증용 이동 입력으로 목표까지 진행했다. 로그: `/tmp/hap3d-voxel-validation-release.log`. Mac Universal 빌드를 만들고 실행 및 Enter/D/Tab/G/R/Esc 입력을 확인했다. Windows 64비트 빌드는 생성했으나 실행은 미검증이다. 초행 사용자 테스트는 하지 않았고 3~5분 목표 달성도 미확인이다.

v0.1.1 최종 검증, Mac Universal·Windows 64-bit 빌드, Mac 실제 입력 흐름, 두 플랫폼 zip 패키징과 공개를 완료했다. 로그: `/tmp/hap3d-minimal-ui-setup-final.log` (`HAP3D_SETUP_OK`), `/tmp/hap3d-minimal-ui-validation-final.log` (`HAP3D_VALIDATION_OK`, 13/13), `/tmp/hap3d-minimal-ui-build-mac-final.log` 및 `/tmp/hap3d-minimal-ui-build-windows-final.log` (`HAP3D_BUILD_OK`). v0.1.1 공개 프리릴리스: https://github.com/ApexLegendIsFun/HAP3D/releases/tag/v0.1.1. Source commit: `9ba5f4b0e279991cc7a56e1ec3126a5d2d8bc232`. Windows 실행과 초행 사용자 테스트는 미검증이다. 글꼴은 Nunito ExtraBold static weight 800, SIL OFL 1.1: https://github.com/google/fonts/tree/main/ofl/nunito; license copy: `Assets/HAP3D/Fonts/OFL-Nunito.txt`.

## 문서·제출 상태 기록

기획서 PDF v0.4는 학교에 제출된 버전이다. 기획 원문 v0.6 (2026.10.13)는 별도 최신 문서이며 제출 완료로 쓰지 않는다. v0.1.0 이력은 저장소 main 커밋 `4f56a3b`와 공개 릴리스 https://github.com/ApexLegendIsFun/HAP3D/releases/tag/v0.1.0 이다. 현재 v0.1.1 소스 커밋 `9ba5f4b0e279991cc7a56e1ec3126a5d2d8bc232`가 원격 main에 있고 공개 릴리스 https://github.com/ApexLegendIsFun/HAP3D/releases/tag/v0.1.1 의 source SHA와 일치한다. 학교 Notion Git Repository 항목 등록 후 재확인 기록: https://app.notion.com/p/3e4caf5650aa80309c09fd9cf7c1705a. Windows 실행·초행 사용자 테스트·3~5분 목표 달성은 미검증이다.

제출 실행파일은 Unity 기준이며 학교 지정 Unity 버전은 미확인이다. 빌드는 실행파일뿐 아니라 폴더 전체와 실제 조작 설명 README.txt를 포함한다. 개발 구현 상태는 README.md와 Docs/작업목록.txt의 확인된 기록을 따른다.
