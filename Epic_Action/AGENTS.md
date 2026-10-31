# Epic_Action 프로젝트 지침

## 무기 아바타 씬 빌더
- 메뉴: `Tools/Epic Action/Build Weapon Avatar Scene (resets Avatar WeaponData)`
- 코드: `Assets/Scripts/Editor/WeaponAvatarSceneBuilder.cs` (Build)
- 결과물: `Assets/Scenes/WeaponAvatarScene.unity`, `Assets/Data/Avatar/` (빌드 때마다 Avatar WeaponData가 초기화된다)

## 데이터·빌더 변경 후 규칙
- 빌더 코드나 WeaponData를 바꾸면 빌더를 다시 실행하는 것까지 한 단위로 처리한다.
- 실행은 `invoke_menu` 대신 `eval`로 `WeaponAvatarSceneBuilder.Build()`를 직접 호출한다. `invoke_menu`는 멈춤·예외와 관계없이 success를 돌려주지만, `eval`은 예외를 `success: false`와 메시지로 돌려준다.
- 보고하기 전에 에셋 수정 시각과 값(`bodyYaw`, `leap` 등)을 확인한다. 반영이 안 됐으면 "수정 완료"라고 쓰지 않는다.
- 모델 구조를 가정하는 코드(파트 이름 검색 등)는 짜기 전에 `eval`로 임포트 결과(렌더러 목록)를 먼저 조회한다.
