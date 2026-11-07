# Odyssey Remote (삼성 오디세이 G50D 아이폰 무선 리모컨) 📱🖥️

> 아이폰을 물리적 리모컨 삼아 삼성 오디세이 G5 G50D 모니터의 **외부입력 전환(PC ↔ 닌텐도 스위치)**과 **화면 밝기 조절**을 무선으로 제어하는 스마트 리모컨 프로젝트입니다.

---

## ✨ 주요 기능

- **원터치 외부입력 전환**
  - 💻 PC 화면 (DisplayPort: `0x0F` / `15`)
  - 🎮 닌텐도 스위치 (HDMI: `0x11` / `17`)
- **실시간 모니터 밝기 조절**
  - 모니터 하드웨어 DDC/CI VCP `0x10` 직접 제어 (0 ~ 50)
  - 미세 조절 버튼 (`-5`, `+5`) 및 실시간 슬라이더 바
  - 프리셋 버튼 (🌙 야간 15 / 💻 표준 35 / ☀️ 최대 50)
- **iOS 맞춤형 리모컨 웹앱 (PWA)**
  - Apple SF Pro 스타일 다크 글래스모피즘 UI
  - 햅틱 진동 피드백
  - Safari [홈 화면에 추가] 시 주소창 없는 단독 앱 모드 동작
- **양방향 전환 안전 테스트 모드**
  - 닌텐도 화면으로 넘겼다가 **5초 후 자동으로 PC 화면으로 복귀**하는 안전 타이머 기능 제공

---

## 🏗️ 시스템 구조

```
[iPhone (Safari / PWA / 단축어)]
              │
       홈 Wi-Fi 로컬 네트워크
              │
              ▼
[Windows PC (Node.js REST API Server :3000)]
              │
       C# Native CLI (MonitorControl.exe)
              │
       DDC/CI (dxva2.dll / Low-Level VCP)
              │
              ▼
[Samsung Odyssey G50D (S27DG500)]
  ├─ 밝기 (VCP 0x10) : 0 ~ 50
  └─ 입력 (VCP 0x60) : DP(15) ↔ HDMI(17)
```

---

## 🚀 빠른 시작

### 1. 실행 요구 사항
- Windows 10 / 11
- Node.js (v18 이상 권장)
- PC와 모니터 간 DDC/CI 지원 케이블 연결 (DP / HDMI)
- PC와 아이폰이 동일한 Wi-Fi 공유기에 연결

### 2. 서버 실행
프로젝트 루트의 `start-remote.bat`을 더블 클릭하여 실행합니다.

```cmd
start-remote.bat
```

콘솔에 표시되는 로컬 IP 주소(`http://192.168.0.x:3000`)를 확인합니다.

### 3. 아이폰 접속 및 앱처럼 등록
1. 아이폰 Safari에서 위 주소(`http://[PC_IP]:3000`)로 접속합니다.
2. Safari 하단 **공유 버튼 [↑]** 터치 → **[홈 화면에 추가]** 터치
3. 홈 화면에 생성된 '오디세이 리모컨' 아이콘으로 네이티브 앱처럼 사용합니다.

---

## 🛠️ CLI 단독 사용법 (`bin/MonitorControl.exe`)

```cmd
# 모니터 상태 확인 (JSON)
MonitorControl.exe status

# 현재 밝기 조회 / 설정
MonitorControl.exe get-brightness
MonitorControl.exe set-brightness 40
MonitorControl.exe brightness-up 5
MonitorControl.exe brightness-down 5

# 입력 소스 조회 / 변경
MonitorControl.exe get-input
MonitorControl.exe set-input 15   # PC (DP)
MonitorControl.exe set-input 17   # 닌텐도 스위치 (HDMI)
```

---

## 📄 라이선스
MIT License
