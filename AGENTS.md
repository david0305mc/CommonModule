# 기본 규칙

## 프로젝트 목적과 사용 관계

- `CommonModule.slnx`는 여러 Unity 프로젝트에서 재사용하는 공통 모듈을 개발하고, Git 저장소를 통해 패키지로 배포하기 위한 프로젝트다.
- 배포 대상 공통 모듈은 `Packages/` 아래에서 관리하며, 다른 프로젝트가 Unity Package Manager의 Git URL 참조로 가져다 사용한다.
- 사용 프로젝트의 예시는 `C:/Dev/MySecretBistro2/MySecretBistro2.slnx`다. 해당 프로젝트의 `Packages/manifest.json`은 이 저장소의 `com.david.datamanager`, `com.david.popupmanager`, `com.david.utils` 패키지를 참조한다.

## 변경 시 원칙

- 공통 모듈은 여러 프로젝트에서 재사용할 수 있도록 유지하고, MySecretBistro2 등 특정 사용 프로젝트의 게임 로직이나 자산에 의존하지 않도록 한다.
- 공개 API, 패키지 구조 및 의존성을 변경할 때는 이 패키지를 사용하는 프로젝트에 미치는 영향과 기존 호환성을 고려한다.
- 공통 기능 수정은 이 저장소의 패키지 원본에 반영한다. 사용 프로젝트의 패키지 캐시를 수정해 해결하지 않는다.
- 사용 사례나 연동 확인이 필요하면 MySecretBistro2의 참조 설정과 호출 코드를 확인한다. 위 로컬 경로는 개발 환경의 참고 위치이며 패키지의 필수 경로로 사용하지 않는다.
