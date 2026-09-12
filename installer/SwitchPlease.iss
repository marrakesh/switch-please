; Inno Setup script for Switch Please.
;
; Built by the release workflow, once per architecture. Everything below is passed in:
;
;   ISCC.exe /DAppVersion=1.0.0 /DArch=x64 /DSourceExe=..\publish\standalone-x64\SwitchPlease.exe
;
; The self-contained build is the payload on purpose. The framework-dependent one is 0.4 MB
; instead of 52, but then the installer has to detect a missing .NET Desktop Runtime, fetch
; it, and cope with that failing on a machine with no network. An installer that cannot fail
; is worth more here than an installer that is small.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef Arch
  #define Arch "x64"
#endif
#ifndef SourceExe
  #define SourceExe "..\publish\standalone-x64\SwitchPlease.exe"
#endif

[Setup]
; Stable across versions and across architectures, so installing a new build upgrades in
; place instead of leaving two entries in Apps & features.
AppId={{8F3A6C21-5D74-4E9B-A1C8-7B2E4F6D9013}
AppName=Switch Please
AppVersion={#AppVersion}
AppVerName=Switch Please {#AppVersion}
VersionInfoVersion={#AppVersion}
AppPublisher=Oleksii Ozerov
AppPublisherURL=https://github.com/marrakesh/switch-please
AppSupportURL=https://github.com/marrakesh/switch-please/issues
AppUpdatesURL=https://github.com/marrakesh/switch-please/releases
LicenseFile=..\LICENSE

; No UAC prompt, and nothing installed for other users. Two reasons, and the second one is
; not cosmetic: Switch Please must not run elevated, because SendInput from a higher
; integrity level cannot reach an ordinary window, so an elevated copy would sit in the tray
; correcting nothing. A per-user install keeps the whole story at one privilege level, and
; the startup entry it writes is per-user anyway.
PrivilegesRequired=lowest
DefaultDirName={autopf}\Switch Please
DefaultGroupName=Switch Please
DisableProgramGroupPage=yes
DisableDirPage=auto

; A tray utility has nothing to configure at install time, so do not pretend otherwise.
DisableReadyPage=no
ShowLanguageDialog=no

UninstallDisplayName=Switch Please
UninstallDisplayIcon={app}\SwitchPlease.exe

; Setup will not run while the program is, and this is the polite half of that: it names the
; running copy rather than failing on a locked file. Program.cs takes this mutex.
AppMutex=SwitchPlease.SingleInstance
CloseApplications=yes
RestartApplications=no

OutputDir=..\installer-output
OutputBaseFilename=SwitchPlease-Setup-{#Arch}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

#if Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "uk"; MessagesFile: "compiler:Languages\Ukrainian.isl"
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "cs"; MessagesFile: "compiler:Languages\Czech.isl"

[CustomMessages]
en.StartupTask=Start Switch Please when Windows starts
ru.StartupTask=Запускать Switch Please вместе с Windows
uk.StartupTask=Запускати Switch Please разом із Windows
de.StartupTask=Switch Please mit Windows starten
cs.StartupTask=Spouštět Switch Please se systémem Windows

en.DesktopIcon=Create a desktop shortcut
ru.DesktopIcon=Создать ярлык на рабочем столе
uk.DesktopIcon=Створити ярлик на робочому столі
de.DesktopIcon=Verknüpfung auf dem Desktop anlegen
cs.DesktopIcon=Vytvořit zástupce na ploše

en.LaunchAfter=Start Switch Please now
ru.LaunchAfter=Запустить Switch Please сейчас
uk.LaunchAfter=Запустити Switch Please зараз
de.LaunchAfter=Switch Please jetzt starten
cs.LaunchAfter=Spustit Switch Please nyní

en.RemoveSettings=Also delete your settings and diagnostic log?
ru.RemoveSettings=Удалить также ваши настройки и журнал диагностики?
uk.RemoveSettings=Видалити також ваші налаштування та журнал діагностики?
de.RemoveSettings=Auch Ihre Einstellungen und das Diagnoseprotokoll löschen?
cs.RemoveSettings=Smazat také vaše nastavení a diagnostický protokol?

[Tasks]
; Checked by default: a layout switcher that has to be started by hand is a layout switcher
; you notice only after typing the sentence wrong.
Name: "startup"; Description: "{cm:StartupTask}"
; Off by default. It lives in the tray; a desktop icon is for launching, which you do once.
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; DestName: "SwitchPlease.exe"; Flags: ignoreversion

[Icons]
Name: "{group}\Switch Please"; Filename: "{app}\SwitchPlease.exe"
Name: "{autodesktop}\Switch Please"; Filename: "{app}\SwitchPlease.exe"; Tasks: desktopicon

[Registry]
; Exactly the value TrayContext writes -- same key, same name, same quoted-path form -- so
; the tray menu's "Start with Windows" reads back as ticked instead of disagreeing with what
; the installer just did. It reads this key, not settings.json, which is what makes that work.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
    ValueName: "SwitchPlease"; ValueData: """{app}\SwitchPlease.exe"""; \
    Flags: uninsdeletevalue; Tasks: startup
; Ticking the task has to mean the program actually starts, and writing the entry above is
; not enough on its own to promise that. Task Manager switches a startup entry off by
; recording it here and leaving the entry itself alone, so an earlier install that the user
; disabled that way would otherwise come back ticked and still not start. Deleting is safe
; in a way the note below is about: it is a different value, in a different key.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"; \
    ValueName: "SwitchPlease"; Flags: deletevalue; Tasks: startup
; Removal is in [Code] rather than a second entry here. A `deletevalue` entry would run at
; install time, in order, and delete the value the entry above had just written.

[Run]
Filename: "{app}\SwitchPlease.exe"; Description: "{cm:LaunchAfter}"; \
    Flags: nowait postinstall skipifsilent

[Code]
// Settings and the diagnostic log live outside {app}, so uninstalling cannot reach them by
// deleting the folder. Ask instead of guessing: a reinstall should keep them, and someone
// removing the program for good should not be left with a stray folder in AppData.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: string;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Unconditionally, whether or not the startup task was ticked at install time: the user
    // may have turned it on from the tray afterwards, and a Run entry pointing at an
    // executable that no longer exists is worse than no entry at all.
    RegDeleteValue(HKEY_CURRENT_USER,
      'Software\Microsoft\Windows\CurrentVersion\Run', 'SwitchPlease');

    // And Task Manager's verdict on that entry, which outlives it. Left behind, it would be
    // waiting for the next install to write the entry again, and would switch it back off.
    RegDeleteValue(HKEY_CURRENT_USER,
      'Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run', 'SwitchPlease');

    DataDir := ExpandConstant('{userappdata}\SwitchPlease');

    if DirExists(DataDir) then
    begin
      if MsgBox(ExpandConstant('{cm:RemoveSettings}'), mbConfirmation, MB_YESNO) = IDYES then
        DelTree(DataDir, True, True, True);
    end;
  end;
end;
