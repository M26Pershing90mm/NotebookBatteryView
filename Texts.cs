using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

internal static class L
{
    public const string Author = "Sakai";
    public const string Copyright = "Copyright © 2026 Sakai. All rights reserved.";
    public const string Version = "1.2.2";
    public static readonly int Language = Detect(CultureInfo.CurrentUICulture.Name);
    static L() { }
    public static string Code { get { return Language == 0 ? "ko" : Language == 2 ? "ja" : "en"; } }
    internal static int Detect(string culture)
    {
        if (String.IsNullOrEmpty(culture)) return 1;
        string primary = culture.Split(new char[] { '-' })[0];
        if (String.Equals(primary, "ko", StringComparison.OrdinalIgnoreCase)) return 0;
        if (String.Equals(primary, "ja", StringComparison.OrdinalIgnoreCase)) return 2;
        return 1;
    }
    private static readonly Dictionary<string, string[]> Strings = new Dictionary<string, string[]>
    {
        { "duration_value", new[] { "{0}시간 {1}분", "{0} h {1} min", "{0}時間{1}分" } },
        { "wear", new[] { "용량 감소율: {0}", "Capacity wear: {0}", "容量の減少率: {0}" } },
        { "pack_remaining", new[] { "선택한 배터리 남은 용량: {0}", "Selected battery capacity: {0}", "選択したバッテリーの残容量: {0}" } },
        { "pack_rate", new[] { "충·방전 전력: {0}", "Charge / discharge rate: {0}", "充電・放電電力: {0}" } },
        { "pack_runtime", new[] { "현재 방전 전력 기준 예상 시간: {0}", "Runtime at current discharge rate: {0}", "現在の放電電力による推定時間: {0}" } },
        { "charging", new[] { "충전 중", "Charging", "充電中" } },
        { "discharging", new[] { "방전 중", "Discharging", "放電中" } },
        { "idle", new[] { "충·방전 중이 아님", "Neither charging nor discharging", "充電・放電していません" } },
        { "pack_note", new[] { "선택한 배터리 기준입니다. 예상 시간은 현재 부하가 계속된다는 가정이며 달라질 수 있습니다. 미지원 값은 추정해 채우지 않습니다.", "For the selected battery. Runtime assumes the current load continues and can vary. Unsupported readings remain unavailable.", "選択したバッテリーの値です。推定時間は現在の負荷が続く場合の目安です。未対応の値は推測しません。" } },
        { "history", new[] { "변화 기록 / CSV", "History / CSV", "推移記録 / CSV" } },
        { "history_title", new[] { "배터리 변화 기록", "Battery session history", "バッテリーの推移記録" } },
        { "history_note", new[] { "이번 실행의 최근 2,000개 기록 · CSV는 전체 배터리 기록 저장", "Latest 2,000 samples this session · CSV saves all battery series.", "今回の起動中の最新2,000件 · CSVは全バッテリーの記録を保存" } },
        { "history_empty", new[] { "아직 기록이 없습니다. 새로고침으로 배터리 정보를 확인하세요.", "No samples yet. Click Refresh to read battery data.", "記録がありません。更新ボタンでバッテリー情報を取得してください。" } },
        { "history_count", new[] { "전체 기록 {0}개", "{0} total samples", "全{0}件" } },
        { "export", new[] { "CSV 저장", "Save CSV", "CSVを保存" } },
        { "export_error", new[] { "CSV를 저장하지 못했습니다. 파일 사용 여부와 폴더 쓰기 권한을 확인하세요.", "Could not save CSV. Check whether the file is open and the folder is writable.", "CSVを保存できません。ファイルの使用状況とフォルダーの書き込み権限を確認してください。" } },
        { "export_done", new[] { "CSV를 저장했습니다.", "CSV saved.", "CSVを保存しました。" } },
        { "history_clear", new[] { "기록 지우기", "Clear history", "記録を消去" } },
        { "pin", new[] { "항상 위에 표시", "Always on top", "常に手前に表示" } },
        { "graph_charge", new[] { "잔량 (%)", "Charge (%)", "残量 (%)" } },
        { "graph_rate", new[] { "충전 + / 방전 − (W) · 실측 전력", "Charge + / Discharge − (W) · Reported rate", "充電 + / 放電 − (W) · 報告された電力" } },
        { "time", new[] { "확인 시각", "Sample time", "取得時刻" } },
        { "state", new[] { "상태", "State", "状態" } },
        { "col_charge", new[] { "배터리 잔량 (%)", "Battery charge (%)", "バッテリー残量 (%)" } },
        { "col_rate", new[] { "전력 (W)", "Rate (W)", "電力 (W)" } },
        { "col_voltage", new[] { "전압 (V)", "Voltage (V)", "電圧 (V)" } },
        { "col_health", new[] { "건강도 (%)", "Health (%)", "劣化状態 (%)" } },
        { "col_capacity", new[] { "남은 용량", "Remaining capacity", "残容量" } },
        { "col_units", new[] { "용량 단위", "Capacity units", "容量単位" } },
        { "col_cycles", new[] { "사이클", "Cycles", "サイクル" } },
        { "title", new[] { "배터리 상태", "Battery status", "バッテリーの状態" } },
        { "heading", new[] { "내 배터리, 한눈에 확인", "Your battery at a glance", "バッテリーの状態をひと目で確認" } },
        { "scope", new[] { "잔량은 PC 전체 기준 · 건강도와 전압은 선택한 배터리 기준", "Charge is for the whole PC; health and voltage are for the selected battery.", "残量はPC全体、劣化状態と電圧は選択したバッテリーの情報です。" } },
        { "charge", new[] { "현재 배터리 잔량", "Current battery charge", "現在のバッテリー残量" } },
        { "health_compact", new[] { "배터리 건강도", "Battery health", "バッテリーの劣化状態" } },
        { "health", new[] { "배터리 건강도 · 완충 용량 ÷ 설계 용량", "Battery health · Full charge ÷ Design capacity", "バッテリーの劣化状態 · 満充電容量 ÷ 設計容量" } },
        { "unknown", new[] { "확인 불가", "Unavailable", "取得できません" } },
        { "name_unknown", new[] { "이름 정보 없음", "Name unavailable", "名称を取得できません" } },
        { "battery_name", new[] { "배터리 {0} · {1}", "Battery {0} · {1}", "バッテリー {0} · {1}" } },
        { "select", new[] { "상세 정보를 볼 배터리 선택", "Select a battery for details", "詳細を表示するバッテリーを選択" } },
        { "no_battery", new[] { "배터리가 감지되지 않습니다. 데스크톱 PC에서는 정상일 수 있습니다.", "No battery detected. This may be normal on a desktop PC.", "バッテリーが検出されません。デスクトップPCでは正常な場合があります。" } },
        { "plugged", new[] { "전원 연결", "Power connected", "電源接続中" } },
        { "on_battery", new[] { "배터리로 사용 중", "Running on battery", "バッテリーで動作中" } },
        { "power_unknown", new[] { "전원 연결 상태 확인 불가", "Power connection status unavailable", "電源の接続状態を取得できません" } },
        { "remaining", new[] { "예상 남은 시간: {0}시간 {1}분 · 사용량에 따라 변동", "Estimated time left: {0} h {1} min · Varies with usage", "推定残り時間: {0}時間{1}分 · 使用状況により変動" } },
        { "remaining_unknown", new[] { "예상 남은 시간: 확인 불가", "Estimated time left: Unavailable", "推定残り時間: 取得できません" } },
        { "health_unknown", new[] { "이 기기에서 건강도를 확인할 수 없습니다.", "Battery health is unavailable on this device.", "この機器ではバッテリーの劣化状態を取得できません。" } },
        { "health_high", new[] { "설계 용량보다 높게 보고되었습니다. 제조 편차나 보정의 영향일 수 있습니다.", "Reported capacity exceeds the design value. Manufacturing variation or calibration may explain this.", "設計容量を超えています。製造上のばらつきや補正の影響が考えられます。" } },
        { "health_good", new[] { "설계 용량의 80% 이상을 유지하고 있습니다.", "At least 80% of the design capacity remains.", "設計容量の80%以上を維持しています。" } },
        { "health_medium", new[] { "용량이 줄었습니다. 실제 사용 시간이 충분한지 확인하세요.", "Capacity has decreased. Check whether the battery runtime meets your needs.", "容量が低下しています。実際の駆動時間が十分か確認してください。" } },
        { "health_low", new[] { "용량이 많이 줄었습니다. 사용 시간이 짧다면 제조사에 점검을 문의하세요.", "Capacity has decreased substantially. Contact the manufacturer if runtime is too short.", "容量が大きく低下しています。駆動時間が短い場合はメーカーに点検を相談してください。" } },
        { "capacity_design", new[] { "설계 용량: {0}", "Design capacity: {0}", "設計容量: {0}" } },
        { "capacity_full", new[] { "완충 용량: {0}", "Full charge capacity: {0}", "満充電容量: {0}" } },
        { "cycles", new[] { "충전 사이클: {0}", "Charge cycles: {0}", "充電サイクル: {0}" } },
        { "cycle_count", new[] { "{0}회", "{0}", "{0}回" } },
        { "driver_units", new[] { "드라이버 단위", "driver units", "ドライバー単位" } },
        { "voltage_current", new[] { "현재 배터리 전압: {0}", "Current battery voltage: {0}", "現在のバッテリー電圧: {0}" } },
        { "voltage_charge", new[] { "충전 중 전압: {0}", "Voltage while charging: {0}", "充電中の電圧: {0}" } },
        { "voltage_discharge", new[] { "방전 중 전압: {0}", "Voltage while discharging: {0}", "放電中の電圧: {0}" } },
        { "not_charging", new[] { "현재 충전 중이 아님", "Not currently charging", "現在は充電していません" } },
        { "not_discharging", new[] { "현재 방전 중이 아님", "Not currently discharging", "現在は放電していません" } },
        { "voltage_note", new[] { "현재 배터리 단자 전압입니다. 충전기 출력 전압이 아니며, 다른 상태의 전압은 추정하지 않습니다.", "This is the battery terminal voltage, not the charger output. Voltage in another state is not estimated.", "バッテリー端子の電圧です。充電器の出力電圧ではありません。別の状態の電圧は推測しません。" } },
        { "health_note", new[] { "건강도는 용량 추정치이며 안전 진단이 아닙니다. 80%·60% 구간은 제조사 교체 판정 기준이 아닙니다.", "Health is a capacity estimate, not a safety diagnosis. The 80% and 60% bands are not manufacturer replacement rules.", "劣化状態は容量による推定で、安全性の診断ではありません。80%・60%の区分はメーカーの交換基準ではありません。" } },
        { "interval_label", new[] { "전압·전력 갱신 간격", "Voltage / rate interval", "電圧・電力の更新間隔" } },
        { "interval_manual", new[] { "수동 (자동 갱신 끄기)", "Manual (automatic updates off)", "手動（自動更新なし）" } },
        { "interval_30", new[] { "30초", "30 seconds", "30秒" } },
        { "interval_60", new[] { "1분", "1 minute", "1分" } },
        { "interval_90", new[] { "1분 30초", "1 minute 30 seconds", "1分30秒" } },
        { "interval_120", new[] { "2분", "2 minutes", "2分" } },
        { "interval_300", new[] { "5분", "5 minutes", "5分" } },
        { "updated_voltage", new[] { "동작 정보 확인: {0}", "Operating data checked: {0}", "動作情報の確認: {0}" } },
        { "voltage_ok", new[] { "배터리 전압·전력·남은 용량을 갱신했습니다.", "Battery voltage, rate and remaining capacity updated.", "バッテリーの電圧・電力・残容量を更新しました。" } },
        { "refresh", new[] { "새로고침", "Refresh", "更新" } },
        { "manual_details", new[] { "배터리 정보를 보려면 새로고침 버튼을 누르세요.", "Click Refresh to read battery information.", "バッテリー情報を確認するには更新ボタンを押してください。" } },
        { "reading", new[] { "배터리 상세 정보를 확인하고 있습니다…", "Reading battery details…", "バッテリーの詳細を取得しています…" } },
        { "read_ok", new[] { "배터리 상세 정보를 확인했습니다.", "Battery details updated.", "バッテリーの詳細を更新しました。" } },
        { "read_none", new[] { "배터리 상세 정보가 없거나 드라이버가 지원하지 않습니다.", "No battery details are available, or the driver does not support them.", "詳細情報がないか、ドライバーが対応していません。" } },
        { "read_partial", new[] { "일부 상세 정보를 읽지 못했습니다. 새로고침 버튼으로 다시 확인하세요.", "Some details could not be read. Click Refresh to retry.", "一部の詳細情報を取得できませんでした。更新ボタンで再度確認してください。" } },
        { "detail_denied", new[] { "상세 정보 접근이 제한되어 있습니다. 시스템 보안 정책을 확인하세요.", "Access to battery details is restricted. Check system security policies.", "詳細情報へのアクセスが制限されています。システムのセキュリティポリシーを確認してください。" } },
        { "detail_error", new[] { "배터리 상세 정보를 읽지 못했습니다. 새로고침 버튼으로 다시 확인하세요.", "Battery details could not be read. Click Refresh to retry.", "バッテリーの詳細を取得できませんでした。更新ボタンで再度確認してください。" } },
        { "live_error", new[] { "현재 전원 정보를 읽지 못했습니다.", "Current power information could not be read.", "現在の電源情報を取得できませんでした。" } },
        { "live_failed", new[] { "현재 잔량 갱신 실패", "Charge update failed", "残量の更新に失敗しました" } },
        { "slow", new[] { "상세 정보 응답이 지연되고 있습니다. 전원·잔량은 계속 갱신되며 창을 닫아도 됩니다.", "Battery details are taking longer than expected. Power and charge continue updating. You can close the window.", "詳細情報の応答が遅れています。電源と残量は引き続き更新されます。ウィンドウを閉じてもかまいません。" } },
        { "waiting", new[] { "드라이버 응답 대기 중입니다. 창을 닫아도 됩니다.", "Waiting for the driver. You can close this window.", "ドライバーの応答を待っています。ウィンドウを閉じてもかまいません。" } },
        { "updated", new[] { "전원·잔량 확인: {0} · 자동 갱신", "Power / charge checked: {0} · Automatic updates", "電源・残量確認: {0} · 自動更新" } },
        { "updated_details", new[] { "상세 정보 확인: {0}", "Details checked: {0}", "詳細情報の確認: {0}" } },
        { "ui_error", new[] { "화면 처리 중 오류가 발생했습니다. 프로그램을 닫고 다시 실행하세요.", "A display error occurred. Close the program and start it again.", "画面の処理中にエラーが発生しました。終了して再度起動してください。" } },
        { "startup_error", new[] { "프로그램을 시작할 수 없습니다. Windows .NET Framework와 시스템 상태를 확인하세요.", "The program could not start. Check Windows .NET Framework and your system.", "起動できません。Windowsの.NET Frameworkとシステムの状態を確認してください。" } },
        { "author", new[] { "제작자: Sakai", "Created by Sakai", "制作者: Sakai" } },
        { "copyright", new[] { "Copyright © 2026 Sakai. 모든 권리 보유.", "Copyright © 2026 Sakai. All rights reserved.", "Copyright © 2026 Sakai. All rights reserved.（すべての権利を留保）" } },
        { "log_title", new[] { "BatteryView 오류 기록", "BatteryView error log", "BatteryView エラーログ" } },
        { "support", new[] { "후원", "Support", "支援" } },
        { "support_error", new[] { "후원 안내를 열 수 없습니다. 기본 브라우저를 확인하거나 https://toon.at/donate/637833976686140024 에 직접 접속하세요.", "Unable to open the support page. Check your default browser or visit https://ko-fi.com/sakai38666.", "支援案内を開けません。既定のブラウザーを確認するか、https://ko-fi.com/sakai38666 にアクセスしてください。" } },
        { "shortcut", new[] { "바탕화면 바로가기 만들기", "Create desktop shortcut", "デスクトップにショートカットを作成" } },
        { "shortcut_done", new[] { "바탕화면에 BatteryView 바로가기를 만들었습니다.", "A BatteryView shortcut was created on your desktop.", "デスクトップにBatteryViewのショートカットを作成しました。" } },
        { "shortcut_error", new[] { "바탕화면 바로가기를 만들지 못했습니다. 바탕화면 쓰기 권한과 보안 정책을 확인하세요.", "The desktop shortcut could not be created. Check desktop write permissions and security policies.", "ショートカットを作成できませんでした。デスクトップへの書き込み権限とセキュリティポリシーを確認してください。" } },
        { "shortcut_collision", new[] { "같은 이름의 다른 바로가기가 이미 있습니다. 기존 파일을 확인한 뒤 다시 시도하세요.", "A different shortcut with the same name already exists. Check the existing file before trying again.", "同じ名前の別のショートカットが存在します。既存のファイルを確認してから再試行してください。" } }
    };
    public static string T(string key) { return Strings[key][Language]; }
    public static string F(string key, params object[] values) { return String.Format(CultureInfo.CurrentCulture, T(key), values); }
    public static void SetCulture()
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(Code == "ko" ? "ko-KR" : Code == "ja" ? "ja-JP" : "en-US");
        System.Threading.Thread.CurrentThread.CurrentCulture = culture;
        System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
    }
    internal static bool Validate()
    {
        foreach (KeyValuePair<string, string[]> pair in Strings)
        {
            if (pair.Value.Length != 3) return false;
            foreach (string value in pair.Value) if (String.IsNullOrWhiteSpace(value)) return false;
        }
        return true;
    }
}

internal static class ErrorLog
{
    private static readonly object Sync = new object();
    private static readonly HashSet<string> Written = new HashSet<string>();
    private static readonly DiagnosticLog Diagnostic = new DiagnosticLog();
    static ErrorLog()
    {
        AppDomain.CurrentDomain.ProcessExit += delegate { lock (Sync) { try { Diagnostic.Dispose(); } catch { } } };
    }
    public static void Write(string code)
    {
        lock (Sync)
        {
            if (!Written.Add(code)) return;
            try
            {
                string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (String.IsNullOrEmpty(profile)) return;
                string root = Path.Combine(profile, "Desktop", "BatteryView-Errors");
                Directory.CreateDirectory(root);
                Diagnostic.Append(root, L.Code, L.T("log_title") + "\r\n" + L.Copyright,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + code + "] " + L.T(code));
            }
            catch { }
        }
    }
}
