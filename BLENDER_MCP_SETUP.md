# Blender MCP setup

설치 및 통합 테스트 기준일: 2026-09-02

## 설치 상태

- Blender: `A:\A_Workspace\Tools\BlenderMCP\Blender521\blender.exe` (Blender 5.2.1 LTS Portable, 공용 설치)
- Blender MCP 애드온: `A:\A_Workspace\Tools\BlenderMCP\Blender521\5.2\scripts\addons\blender_mcp.py`
- MCP 실행기: `C:\Users\충충\.local\bin\uvx.exe`
- 공용 uv 캐시: `A:\A_Workspace\Tools\BlenderMCP\uv-cache`
- MCP Python: 3.11 고정
- 연결: `127.0.0.1:9876`
- Safe Mode: 활성화
- 텔레메트리: 비활성화
- 사용자 PATH: 공용 Blender 폴더와 `start-blender-mcp.cmd` 폴더 등록

Codex MCP 설정은 사용자 설정 파일 `C:\Users\충충\.codex\config.toml`의 `[mcp_servers.blender]`에 반영되어 있습니다. Codex 앱이 이미 실행 중이었다면 완전히 재시작해야 새 MCP 서버가 도구 목록에 나타납니다. Blender MCP 서버와 Blender 인스턴스는 포트 충돌을 피하기 위해 한 개씩만 실행합니다.

## 실행

```powershell
start-blender-mcp.cmd

# 특정 blend 파일을 열면서 MCP를 시작
start-blender-mcp.cmd 'A:\A_Workspace\_Unity\.tools\blender_mcp_sample_scene.blend'
```

`blender_mcp_bootstrap.py`가 애드온을 등록하고 MCP TCP 서버를 자동으로 시작합니다. 일반적인 Blender UI 경로를 사용하려면 3D View에서 `N` → MCP for Blender 탭 → 서버 시작을 선택해도 됩니다.

기존 프로젝트 안의 portable 복사본은 롤백용으로 남겨 두었지만, Codex 설정과 실행 경로는 모두 `A:\A_Workspace\Tools\BlenderMCP\` 공용 설치를 가리킵니다.

## 통합 테스트 결과

MCP 표준 입출력 클라이언트로 다음 호출을 통과시켰습니다.

1. `get_addon_status`: protocol 5, addon 1.6, Blender 5.2.1 LTS, up-to-date
2. `get_scene_info`
3. `execute_blender_code`: Ground/Cube/Sphere/Cylinder/Torus 생성
4. `execute_blender_code`: 카메라·조명 설정, 저장, 렌더
5. `get_scene_info` 및 `get_object_info`
6. `get_viewport_screenshot`

결과 파일:

- `A:\A_Workspace\_Unity\.tools\blender_mcp_sample_scene.blend`
- `A:\A_Workspace\_Unity\.tools\blender_mcp_sample_render.png`
- `A:\A_Workspace\_Unity\.tools\blender_mcp_viewport.png`
- `A:\A_Workspace\_Unity\.tools\blender_mcp_smoke_report.json`
