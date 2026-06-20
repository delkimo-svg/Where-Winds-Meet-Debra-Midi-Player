#!/usr/bin/env python3
"""Merge missing keys from en.json into locale files with translations."""
import json
import os

DIR = os.path.join(
    os.path.dirname(__file__), "..", "src", "WhereWindsMeetMidiPlayer", "Assets", "Localization"
)

CHROME_TRANSPORT_TIPS = {
    "fr": {
        "Chrome_ShuffleTip": "Aléatoire",
        "Chrome_RepeatTip": "Répéter",
        "Chrome_SeekTip": "Cliquer pour chercher",
        "Chrome_VolumePcTip": "Volume PC (audio du jeu)",
        "Chrome_VolumeMasterTip": "Contrôle le volume principal Windows",
    },
    "de": {
        "Chrome_ShuffleTip": "Zufall",
        "Chrome_RepeatTip": "Wiederholen",
        "Chrome_SeekTip": "Klicken zum Springen",
        "Chrome_VolumePcTip": "PC-Lautstärke (Spiel-Audio)",
        "Chrome_VolumeMasterTip": "Steuert die Windows-Gesamtlautstärke",
    },
    "es": {
        "Chrome_ShuffleTip": "Aleatorio",
        "Chrome_RepeatTip": "Repetir",
        "Chrome_SeekTip": "Clic para buscar",
        "Chrome_VolumePcTip": "Volumen del PC (audio del juego)",
        "Chrome_VolumeMasterTip": "Controla el volumen maestro de Windows",
    },
    "it": {
        "Chrome_ShuffleTip": "Casuale",
        "Chrome_RepeatTip": "Ripeti",
        "Chrome_SeekTip": "Clic per cercare",
        "Chrome_VolumePcTip": "Volume PC (audio di gioco)",
        "Chrome_VolumeMasterTip": "Controlla il volume master di Windows",
    },
    "pt": {
        "Chrome_ShuffleTip": "Aleatório",
        "Chrome_RepeatTip": "Repetir",
        "Chrome_SeekTip": "Clique para buscar",
        "Chrome_VolumePcTip": "Volume do PC (áudio do jogo)",
        "Chrome_VolumeMasterTip": "Controla o volume principal do Windows",
    },
    "zh": {
        "Chrome_ShuffleTip": "随机",
        "Chrome_RepeatTip": "重复",
        "Chrome_SeekTip": "点击定位",
        "Chrome_VolumePcTip": "电脑音量（游戏音频）",
        "Chrome_VolumeMasterTip": "控制 Windows 主音量",
    },
    "ja": {
        "Chrome_ShuffleTip": "シャッフル",
        "Chrome_RepeatTip": "リピート",
        "Chrome_SeekTip": "クリックでシーク",
        "Chrome_VolumePcTip": "PC 音量（ゲーム音声）",
        "Chrome_VolumeMasterTip": "Windows のマスター音量を調整",
    },
    "ar": {
        "Chrome_ShuffleTip": "عشوائي",
        "Chrome_RepeatTip": "تكرار",
        "Chrome_SeekTip": "انقر للانتقال",
        "Chrome_VolumePcTip": "صوت الكمبيوتر (صوت اللعبة)",
        "Chrome_VolumeMasterTip": "يضبط الصوت الرئيسي لـ Windows",
    },
    "vi": {
        "Chrome_ShuffleTip": "Ngẫu nhiên",
        "Chrome_RepeatTip": "Lặp lại",
        "Chrome_SeekTip": "Nhấp để tìm vị trí",
        "Chrome_VolumePcTip": "Âm lượng PC (âm thanh game)",
        "Chrome_VolumeMasterTip": "Điều chỉnh âm lượng chính của Windows",
    },
}

TRANSLATIONS = {
    "fr": {
        "Header_Update": "Mise à jour",
        "Header_Update_Tooltip": "Une nouvelle version portable est disponible",
        "Settings_ReleaseManifest": "URL du manifeste de mise à jour (JSON CDN Discord)",
        "Settings_ReleaseManifest_Hint": "URL optionnelle. Si vide, l'app lit le manifeste Discord (discord-catalogue.json). Vous pouvez aussi placer debra-update-manifest.url à côté de l'exe.",
        "UpdateOverlay_Title": "Mise à jour disponible — v{0}",
        "UpdateOverlay_Subtitle": "Téléchargez le .rar portable dans ce dossier, puis extrayez sur votre installation actuelle.",
        "UpdateOverlay_Download": "Télécharger .rar",
        "UpdateOverlay_Later": "Me rappeler plus tard",
        "UpdateOverlay_OpenFolder": "Ouvrir le dossier",
        "UpdateOverlay_Close": "Fermer",
        "UpdateOverlay_Ready": "Prêt à télécharger la dernière archive portable.",
        "UpdateOverlay_Downloading": "Téléchargement…",
        "UpdateOverlay_Saved": "{0} enregistré. Extrayez avec WinRAR ou 7-Zip dans ce dossier, puis lancez DebraMidiPlayer.exe.",
        "UpdateOverlay_Failed": "Échec du téléchargement : {0}",
        "UpdateOverlay_ExtractHint": "Les mises à jour portables restent dans un seul dossier : DebraMidiPlayer.exe, Assets et discord-catalogue.json doivent rester ensemble. Remplacez les fichiers lors de l'extraction du .rar.",
    },
    "de": {
        "Library_Clear": "Leeren",
        "Library_ClearTip": "Alle Songs aus der Bibliothek entfernen",
        "Library_ClearTitle": "Bibliothek leeren?",
        "Library_ClearMessage": "Alle {0} Songs aus der Bibliothek entfernen? Playlists und MIDI-Dateien auf der Festplatte bleiben erhalten.",
        "Playlist_CreateTip": "Leere Playlist mit diesem Namen erstellen (Enter)",
        "Settings_PlayerHotkeys": "Player-Hotkeys",
        "Settings_PlayerHotkeysHint": "Aktiv, wenn das Spiel oder Debra im Fokus ist. Feld anklicken, dann Taste drücken (Esc zum Abbrechen).",
        "Settings_HotkeyPlayPause": "Play / Pause",
        "Settings_HotkeyStop": "Stopp",
        "Settings_HotkeyPrevious": "Vorheriger Track",
        "Settings_HotkeyNext": "Nächster Track",
        "Settings_HotkeyReset": "Auf F3–F6 zurücksetzen",
        "Previous_Tooltip": "Vorheriger Track",
        "Next_Tooltip": "Nächster Track",
        "Header_Update": "Update",
        "Header_Update_Tooltip": "Eine neuere Portable-Version ist verfügbar",
        "Settings_ReleaseManifest": "Update-Manifest-URL (Discord CDN JSON)",
        "Settings_ReleaseManifest_Hint": "Optionale URL. Wenn leer, liest die App das Discord-Manifest (discord-catalogue.json). debra-update-manifest.url kann auch neben der exe liegen.",
        "UpdateOverlay_Title": "Update verfügbar — v{0}",
        "UpdateOverlay_Subtitle": "Portable .rar in diesen Ordner laden und über die aktuelle Installation extrahieren.",
        "UpdateOverlay_Download": ".rar herunterladen",
        "UpdateOverlay_Later": "Später erinnern",
        "UpdateOverlay_OpenFolder": "Ordner öffnen",
        "UpdateOverlay_Close": "Schließen",
        "UpdateOverlay_Ready": "Bereit zum Download der neuesten Portable-Archiv.",
        "UpdateOverlay_Downloading": "Wird heruntergeladen…",
        "UpdateOverlay_Saved": "{0} gespeichert. Mit WinRAR oder 7-Zip in diesen Ordner extrahieren, dann DebraMidiPlayer.exe starten.",
        "UpdateOverlay_Failed": "Download fehlgeschlagen: {0}",
        "UpdateOverlay_ExtractHint": "Portable Updates bleiben ein Ordner: DebraMidiPlayer.exe, Assets und discord-catalogue.json zusammenhalten. Dateien beim Extrahieren der .rar überschreiben.",
        "Chrome_PlayerOpacityTip": "Macht das Fenster durchsichtig, damit du das Spiel dahinter siehst. Minimum 15 % für Hintergrund, Karten, Menü, Kopfzeile und Player.",
    },
    "es": {
        "Library_Clear": "Vaciar",
        "Library_ClearTip": "Quitar todas las canciones de la biblioteca",
        "Library_ClearTitle": "¿Vaciar biblioteca?",
        "Library_ClearMessage": "¿Quitar las {0} canciones de la biblioteca? Las listas y los archivos MIDI en disco no se eliminan.",
        "Playlist_CreateTip": "Crear lista vacía con este nombre (Enter)",
        "Settings_PlayerHotkeys": "Atajos del reproductor",
        "Settings_PlayerHotkeysHint": "Activos cuando el juego o Debra está en foco. Clic en un campo y pulsa la tecla (Esc para cancelar).",
        "Settings_HotkeyPlayPause": "Reproducir / Pausa",
        "Settings_HotkeyStop": "Detener",
        "Settings_HotkeyPrevious": "Pista anterior",
        "Settings_HotkeyNext": "Pista siguiente",
        "Settings_HotkeyReset": "Restablecer a F3–F6",
        "Previous_Tooltip": "Pista anterior",
        "Next_Tooltip": "Pista siguiente",
        "Header_Update": "Actualizar",
        "Header_Update_Tooltip": "Hay una versión portable más reciente",
        "Settings_ReleaseManifest": "URL del manifest de actualización (JSON CDN Discord)",
        "Settings_ReleaseManifest_Hint": "URL opcional. Si está vacía, la app lee el manifest de Discord (discord-catalogue.json). También puedes poner debra-update-manifest.url junto al exe.",
        "UpdateOverlay_Title": "Actualización disponible — v{0}",
        "UpdateOverlay_Subtitle": "Descarga el .rar portable en esta carpeta y extrae sobre tu instalación actual.",
        "UpdateOverlay_Download": "Descargar .rar",
        "UpdateOverlay_Later": "Recordarme más tarde",
        "UpdateOverlay_OpenFolder": "Abrir carpeta",
        "UpdateOverlay_Close": "Cerrar",
        "UpdateOverlay_Ready": "Listo para descargar el archivo portable más reciente.",
        "UpdateOverlay_Downloading": "Descargando…",
        "UpdateOverlay_Saved": "Guardado {0}. Extrae con WinRAR o 7-Zip en esta carpeta y ejecuta DebraMidiPlayer.exe.",
        "UpdateOverlay_Failed": "Error de descarga: {0}",
        "UpdateOverlay_ExtractHint": "Las actualizaciones portable son una sola carpeta: DebraMidiPlayer.exe, Assets y discord-catalogue.json deben permanecer juntos. Sobrescribe al extraer el .rar.",
        "Chrome_PlayerOpacityTip": "Hace la ventana transparente para ver el juego detrás. Mínimo 15 % en fondo, cartas, menú, cabecera y reproductor.",
    },
    "it": {
        "Library_Clear": "Svuota",
        "Library_ClearTip": "Rimuovi tutti i brani dalla libreria",
        "Library_ClearTitle": "Svuotare la libreria?",
        "Library_ClearMessage": "Rimuovere tutti i {0} brani dalla libreria? Le playlist e i file MIDI su disco non vengono eliminati.",
        "Playlist_CreateTip": "Crea playlist vuota con questo nome (Invio)",
        "Settings_PlayerHotkeys": "Hotkey del player",
        "Settings_PlayerHotkeysHint": "Attivi quando il gioco o Debra è a fuoco. Clic sul campo, poi premi la chiave (Esc per annullare).",
        "Settings_HotkeyPlayPause": "Play / Pausa",
        "Settings_HotkeyStop": "Stop",
        "Settings_HotkeyPrevious": "Brano precedente",
        "Settings_HotkeyNext": "Brano successivo",
        "Settings_HotkeyReset": "Ripristina F3–F6",
        "Previous_Tooltip": "Brano precedente",
        "Next_Tooltip": "Brano successivo",
        "Header_Update": "Aggiorna",
        "Header_Update_Tooltip": "È disponibile una build portable più recente",
        "Settings_ReleaseManifest": "URL manifest aggiornamenti (JSON CDN Discord)",
        "Settings_ReleaseManifest_Hint": "URL opzionale. Se vuoto, l'app legge il manifest Discord (discord-catalogue.json). Puoi anche mettere debra-update-manifest.url accanto all'exe.",
        "UpdateOverlay_Title": "Aggiornamento disponibile — v{0}",
        "UpdateOverlay_Subtitle": "Scarica il .rar portable in questa cartella, poi estrai sulla installazione attuale.",
        "UpdateOverlay_Download": "Scarica .rar",
        "UpdateOverlay_Later": "Ricordami più tardi",
        "UpdateOverlay_OpenFolder": "Apri cartella",
        "UpdateOverlay_Close": "Chiudi",
        "UpdateOverlay_Ready": "Pronto per scaricare l'archivio portable più recente.",
        "UpdateOverlay_Downloading": "Download in corso…",
        "UpdateOverlay_Saved": "Salvato {0}. Estrai con WinRAR o 7-Zip in questa cartella, poi avvia DebraMidiPlayer.exe.",
        "UpdateOverlay_Failed": "Download non riuscito: {0}",
        "UpdateOverlay_ExtractHint": "Gli aggiornamenti portable sono una cartella: DebraMidiPlayer.exe, Assets e discord-catalogue.json devono stare insieme. Sovrascrivi i file estraendo il .rar.",
        "Chrome_PlayerOpacityTip": "Rende la finestra trasparente per vedere il gioco dietro. Minimo 15 % su sfondo, carte, menu, intestazione e player.",
    },
    "pt": {
        "Library_Clear": "Limpar",
        "Library_ClearTip": "Remover todas as músicas da biblioteca",
        "Library_ClearTitle": "Limpar biblioteca?",
        "Library_ClearMessage": "Remover todas as {0} músicas da biblioteca? Playlists e ficheiros MIDI no disco não são apagados.",
        "Playlist_CreateTip": "Criar playlist vazia com este nome (Enter)",
        "Settings_PlayerHotkeys": "Atalhos do player",
        "Settings_PlayerHotkeysHint": "Ativos quando o jogo ou Debra está em foco. Clique num campo e pressione a tecla (Esc para cancelar).",
        "Settings_HotkeyPlayPause": "Reproduzir / Pausa",
        "Settings_HotkeyStop": "Parar",
        "Settings_HotkeyPrevious": "Faixa anterior",
        "Settings_HotkeyNext": "Faixa seguinte",
        "Settings_HotkeyReset": "Repor F3–F6",
        "Previous_Tooltip": "Faixa anterior",
        "Next_Tooltip": "Faixa seguinte",
        "Header_Update": "Atualizar",
        "Header_Update_Tooltip": "Uma versão portable mais recente está disponível",
        "Settings_ReleaseManifest": "URL do manifest de atualização (JSON CDN Discord)",
        "Settings_ReleaseManifest_Hint": "URL opcional. Se vazio, a app lê o manifest Discord (discord-catalogue.json). Também pode colocar debra-update-manifest.url junto ao exe.",
        "UpdateOverlay_Title": "Atualização disponível — v{0}",
        "UpdateOverlay_Subtitle": "Baixe o .rar portable nesta pasta e extraia sobre a instalação atual.",
        "UpdateOverlay_Download": "Baixar .rar",
        "UpdateOverlay_Later": "Lembrar mais tarde",
        "UpdateOverlay_OpenFolder": "Abrir pasta",
        "UpdateOverlay_Close": "Fechar",
        "UpdateOverlay_Ready": "Pronto para baixar o arquivo portable mais recente.",
        "UpdateOverlay_Downloading": "Baixando…",
        "UpdateOverlay_Saved": "Salvo {0}. Extraia com WinRAR ou 7-Zip nesta pasta e execute DebraMidiPlayer.exe.",
        "UpdateOverlay_Failed": "Falha no download: {0}",
        "UpdateOverlay_ExtractHint": "Atualizações portable ficam numa pasta: DebraMidiPlayer.exe, Assets e discord-catalogue.json devem ficar juntos. Substitua os ficheiros ao extrair o .rar.",
        "Chrome_PlayerOpacityTip": "Torna a janela transparente para ver o jogo atrás. Mínimo 15 % no fundo, cartões, menu, cabeçalho e player.",
    },
    "zh": {
        "Library_Clear": "清空",
        "Library_ClearTip": "从曲库中移除所有歌曲",
        "Library_ClearTitle": "清空曲库？",
        "Library_ClearMessage": "从曲库中移除全部 {0} 首歌曲？播放列表和磁盘上的 MIDI 文件不受影响。",
        "Playlist_CreateTip": "用此名称创建空播放列表 (Enter)",
        "Settings_PlayerHotkeys": "播放器快捷键",
        "Settings_PlayerHotkeysHint": "在游戏或 Debra 窗口聚焦时生效。点击输入框后按下新按键（Esc 取消）。",
        "Settings_HotkeyPlayPause": "播放 / 暂停",
        "Settings_HotkeyStop": "停止",
        "Settings_HotkeyPrevious": "上一首",
        "Settings_HotkeyNext": "下一首",
        "Settings_HotkeyReset": "重置为 F3–F6",
        "Previous_Tooltip": "上一首",
        "Next_Tooltip": "下一首",
        "Header_Update": "更新",
        "Header_Update_Tooltip": "有新的便携版可用",
        "Settings_ReleaseManifest": "更新清单 URL（Discord CDN JSON）",
        "Settings_ReleaseManifest_Hint": "可选覆盖 URL。若为空，应用从 Discord 读取清单 (discord-catalogue.json)。也可在 exe 旁放置 debra-update-manifest.url。",
        "UpdateOverlay_Title": "有可用更新 — v{0}",
        "UpdateOverlay_Subtitle": "将便携 .rar 下载到此文件夹，然后解压覆盖当前安装。",
        "UpdateOverlay_Download": "下载 .rar",
        "UpdateOverlay_Later": "稍后提醒",
        "UpdateOverlay_OpenFolder": "打开文件夹",
        "UpdateOverlay_Close": "关闭",
        "UpdateOverlay_Ready": "准备下载最新便携压缩包。",
        "UpdateOverlay_Downloading": "正在下载…",
        "UpdateOverlay_Saved": "已保存 {0}。用 WinRAR 或 7-Zip 解压到此文件夹，然后运行 DebraMidiPlayer.exe。",
        "UpdateOverlay_Failed": "下载失败：{0}",
        "UpdateOverlay_ExtractHint": "便携更新保持单文件夹：DebraMidiPlayer.exe、Assets 和 discord-catalogue.json 须在一起。解压 .rar 时覆盖文件。",
        "Chrome_PlayerOpacityTip": "使窗口透明以查看背后的游戏。背景、卡片、菜单、标题栏和播放器最低 15%。",
    },
    "ja": {
        "Library_Clear": "クリア",
        "Library_ClearTip": "ライブラリからすべての曲を削除",
        "Library_ClearTitle": "ライブラリをクリア？",
        "Library_ClearMessage": "ライブラリから {0} 曲をすべて削除しますか？プレイリストとディスク上の MIDI は削除されません。",
        "Playlist_CreateTip": "この名前で空のプレイリストを作成 (Enter)",
        "Settings_PlayerHotkeys": "プレイヤーホットキー",
        "Settings_PlayerHotkeysHint": "ゲームまたは Debra にフォーカスがあるとき有効。欄をクリックしてキーを押す（Esc でキャンセル）。",
        "Settings_HotkeyPlayPause": "再生 / 一時停止",
        "Settings_HotkeyStop": "停止",
        "Settings_HotkeyPrevious": "前の曲",
        "Settings_HotkeyNext": "次の曲",
        "Settings_HotkeyReset": "F3–F6 にリセット",
        "Previous_Tooltip": "前の曲",
        "Next_Tooltip": "次の曲",
        "Header_Update": "更新",
        "Header_Update_Tooltip": "新しいポータブル版が利用可能です",
        "Settings_ReleaseManifest": "更新マニフェスト URL（Discord CDN JSON）",
        "Settings_ReleaseManifest_Hint": "任意の URL。空の場合は Discord のマニフェスト (discord-catalogue.json) を読み込みます。exe の横に debra-update-manifest.url も置けます。",
        "UpdateOverlay_Title": "更新あり — v{0}",
        "UpdateOverlay_Subtitle": "ポータブル .rar をこのフォルダにダウンロードし、現在のインストールに上書き展開してください。",
        "UpdateOverlay_Download": ".rar をダウンロード",
        "UpdateOverlay_Later": "後で通知",
        "UpdateOverlay_OpenFolder": "フォルダを開く",
        "UpdateOverlay_Close": "閉じる",
        "UpdateOverlay_Ready": "最新のポータブルアーカイブをダウンロードする準備ができました。",
        "UpdateOverlay_Downloading": "ダウンロード中…",
        "UpdateOverlay_Saved": "{0} を保存しました。WinRAR または 7-Zip でこのフォルダに展開し、DebraMidiPlayer.exe を実行してください。",
        "UpdateOverlay_Failed": "ダウンロード失敗：{0}",
        "UpdateOverlay_ExtractHint": "ポータブル更新は単一フォルダのまま：DebraMidiPlayer.exe、Assets、discord-catalogue.json を一緒に保ち、.rar 展開時にファイルを上書きしてください。",
        "Chrome_PlayerOpacityTip": "ウィンドウを透過させ、背後のゲームを見えます。背景・カード・メニュー・ヘッダー・プレイヤーは最低 15%。",
    },
    "ar": {
        "Library_Clear": "مسح",
        "Library_ClearTip": "إزالة كل الأغاني من المكتبة",
        "Library_ClearTitle": "مسح المكتبة؟",
        "Library_ClearMessage": "إزالة كل {0} أغنية من المكتبة؟ القوائم وملفات MIDI على القرص لا تُحذف.",
        "Playlist_CreateTip": "إنشاء قائمة فارغة بهذا الاسم (Enter)",
        "Settings_PlayerHotkeys": "اختصارات المشغّل",
        "Settings_PlayerHotkeysHint": "تعمل عند تركيز اللعبة أو Debra. انقر الحقل ثم اضغط المفتاح (Esc للإلغاء).",
        "Settings_HotkeyPlayPause": "تشغيل / إيقاف مؤقت",
        "Settings_HotkeyStop": "إيقاف",
        "Settings_HotkeyPrevious": "المسار السابق",
        "Settings_HotkeyNext": "المسار التالي",
        "Settings_HotkeyReset": "إعادة إلى F3–F6",
        "Previous_Tooltip": "المسار السابق",
        "Next_Tooltip": "المسار التالي",
        "Header_Update": "تحديث",
        "Header_Update_Tooltip": "يتوفر إصدار portable أحدث",
        "Settings_ReleaseManifest": "رابط manifest التحديث (JSON CDN Discord)",
        "Settings_ReleaseManifest_Hint": "رابط اختياري. إذا كان فارغًا، يقرأ التطبيق manifest Discord (discord-catalogue.json). يمكنك أيضًا وضع debra-update-manifest.url بجانب exe.",
        "UpdateOverlay_Title": "تحديث متاح — v{0}",
        "UpdateOverlay_Subtitle": "حمّل .rar portable في هذا المجلد ثم استخرج فوق التثبيت الحالي.",
        "UpdateOverlay_Download": "تحميل .rar",
        "UpdateOverlay_Later": "ذكّرني لاحقًا",
        "UpdateOverlay_OpenFolder": "فتح المجلد",
        "UpdateOverlay_Close": "إغلاق",
        "UpdateOverlay_Ready": "جاهز لتحميل أحدث أرشيف portable.",
        "UpdateOverlay_Downloading": "جارٍ التحميل…",
        "UpdateOverlay_Saved": "تم حفظ {0}. استخرج بـ WinRAR أو 7-Zip في هذا المجلد ثم شغّل DebraMidiPlayer.exe.",
        "UpdateOverlay_Failed": "فشل التحميل: {0}",
        "UpdateOverlay_ExtractHint": "تحديثات portable تبقى مجلدًا واحدًا: DebraMidiPlayer.exe و Assets و discord-catalogue.json معًا. استبدل الملفات عند استخراج .rar.",
        "Chrome_PlayerOpacityTip": "يجعل النافذة شفافة لرؤية اللعبة خلفها. الحد الأدنى 15% للخلفية والبطاقات والقائمة والرأس والمشغّل.",
    },
    "vi": {
        "Settings_PlayerHotkeys": "Phím nóng trình phát",
        "Settings_PlayerHotkeysHint": "Hoạt động khi game hoặc Debra được focus. Nhấp ô, rồi bấm phím (Esc để hủy).",
        "Settings_HotkeyPlayPause": "Phát / Tạm dừng",
        "Settings_HotkeyStop": "Dừng",
        "Settings_HotkeyPrevious": "Bài trước",
        "Settings_HotkeyNext": "Bài sau",
        "Settings_HotkeyReset": "Khôi phục F3–F6",
        "Previous_Tooltip": "Bài trước",
        "Next_Tooltip": "Bài sau",
        "Header_Update": "Cập nhật",
        "Header_Update_Tooltip": "Có bản portable mới",
        "Settings_ReleaseManifest": "URL manifest cập nhật (JSON CDN Discord)",
        "Settings_ReleaseManifest_Hint": "URL tùy chọn. Nếu trống, app đọc manifest Discord (discord-catalogue.json). Có thể đặt debra-update-manifest.url cạnh exe.",
        "UpdateOverlay_Title": "Có bản cập nhật — v{0}",
        "UpdateOverlay_Subtitle": "Tải .rar portable vào thư mục này, rồi giải nén lên bản cài hiện tại.",
        "UpdateOverlay_Download": "Tải .rar",
        "UpdateOverlay_Later": "Nhắc sau",
        "UpdateOverlay_OpenFolder": "Mở thư mục",
        "UpdateOverlay_Close": "Đóng",
        "UpdateOverlay_Ready": "Sẵn sàng tải bản portable mới nhất.",
        "UpdateOverlay_Downloading": "Đang tải…",
        "UpdateOverlay_Saved": "Đã lưu {0}. Giải nén bằng WinRAR hoặc 7-Zip vào thư mục này, rồi chạy DebraMidiPlayer.exe.",
        "UpdateOverlay_Failed": "Tải thất bại: {0}",
        "UpdateOverlay_ExtractHint": "Cập nhật portable là một thư mục: DebraMidiPlayer.exe, Assets và discord-catalogue.json phải ở cùng chỗ. Ghi đè file khi giải nén .rar.",
        "Playlist_CreateTip": "Tạo danh sách trống với tên này (Enter)",
        "Chrome_PlayerOpacityTip": "Làm cửa sổ trong suốt để thấy game phía sau. Tối thiểu 15% cho nền, thẻ, menu, header và player.",
    },
}

UPDATES_ONLY = {
    "de": {"Chrome_PlayerOpacityTip": TRANSLATIONS["de"]["Chrome_PlayerOpacityTip"]},
    "es": {"Chrome_PlayerOpacityTip": TRANSLATIONS["es"]["Chrome_PlayerOpacityTip"]},
    "it": {"Chrome_PlayerOpacityTip": TRANSLATIONS["it"]["Chrome_PlayerOpacityTip"]},
    "pt": {"Chrome_PlayerOpacityTip": TRANSLATIONS["pt"]["Chrome_PlayerOpacityTip"]},
    "zh": {"Chrome_PlayerOpacityTip": TRANSLATIONS["zh"]["Chrome_PlayerOpacityTip"]},
    "ja": {"Chrome_PlayerOpacityTip": TRANSLATIONS["ja"]["Chrome_PlayerOpacityTip"]},
    "ar": {"Chrome_PlayerOpacityTip": TRANSLATIONS["ar"]["Chrome_PlayerOpacityTip"]},
    "vi": {"Chrome_PlayerOpacityTip": TRANSLATIONS["vi"]["Chrome_PlayerOpacityTip"]},
}


def load_json(path):
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def save_json(path, data, indent_style):
    """indent_style: 2 for most, 4 for vi/en with extra spaces."""
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        if indent_style == 4:
            json.dump(data, f, ensure_ascii=False, indent=4)
            f.write("\n")
        else:
            json.dump(data, f, ensure_ascii=False, indent=2)
            f.write("\n")


def main():
    en_path = os.path.join(DIR, "en.json")
    en = load_json(en_path)
    en_keys = list(en.keys())

    for lang in ["fr", "de", "es", "it", "pt", "zh", "ja", "ar", "vi"]:
        path = os.path.join(DIR, f"{lang}.json")
        data = load_json(path)

        additions = TRANSLATIONS.get(lang, {})
        additions = {**additions, **CHROME_TRANSPORT_TIPS.get(lang, {})}
        for key, value in additions.items():
            data[key] = value

        # Fill any remaining gaps from English
        for key in en_keys:
            if key not in data or not str(data.get(key, "")).strip():
                if key in additions:
                    continue
                data[key] = en[key]

        # Reorder to match en key order, then append any extra keys
        ordered = {}
        for key in en_keys:
            if key in data:
                ordered[key] = data[key]
        for key in sorted(data.keys()):
            if key not in ordered:
                ordered[key] = data[key]

        indent = 4 if lang == "vi" else 2
        save_json(path, ordered, indent)
        missing = [k for k in en_keys if k not in ordered]
        print(f"{lang}: {len(ordered)} keys, missing {len(missing)}")

    print("Done.")


if __name__ == "__main__":
    main()
