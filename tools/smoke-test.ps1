# Verifie que l'application demarre et affiche reellement sa fenetre.
#
# Un processus vivant ne prouve rien : si une ressource XAML manque, WPF leve une
# exception au chargement, la fenetre principale n'est jamais creee et il ne reste
# qu'une boite d'erreur. C'est exactement ce qui est arrive avec Window.Icon pointant
# sur un fichier absent des ressources embarquees.
#
# Usage : powershell -File tools/smoke-test.ps1 [chemin\vers\FoundMySave.exe]

param(
    [string]$Exe = (Join-Path (Split-Path $PSScriptRoot -Parent) 'publish\FoundMySave.exe'),
    [int]$TimeoutSeconds = 30
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Exe)) {
    Write-Host "ECHEC : introuvable, $Exe"
    exit 1
}

Write-Host "Lancement de $Exe"
$proc = Start-Process $Exe -PassThru

try {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $handle = [IntPtr]::Zero
    $title = ''

    while ((Get-Date) -lt $deadline) {
        if ($proc.HasExited) {
            Write-Host "ECHEC : l'application s'est fermee seule, code $($proc.ExitCode)"
            exit 1
        }

        $proc.Refresh()
        if ($proc.MainWindowHandle -ne [IntPtr]::Zero) {
            $handle = $proc.MainWindowHandle
            $title = $proc.MainWindowTitle
            break
        }

        Start-Sleep -Milliseconds 400
    }

    if ($handle -eq [IntPtr]::Zero) {
        Write-Host "ECHEC : aucune fenetre principale apres $TimeoutSeconds s."
        Write-Host "        Une boite d'erreur a probablement remplace la fenetre :"
        Write-Host "        ressource XAML manquante, ou exception au chargement."
        exit 1
    }

    if ($title -notlike 'FoundMySave*') {
        Write-Host "ECHEC : titre inattendu, '$title'"
        exit 1
    }

    # Une fenetre creee puis detruite aussitot signale une erreur differee.
    Start-Sleep -Seconds 3
    $proc.Refresh()
    if ($proc.HasExited) {
        Write-Host "ECHEC : la fenetre est apparue puis l'application s'est fermee."
        exit 1
    }

    Write-Host "OK : fenetre affichee, titre '$title'"
    exit 0
}
finally {
    # Fermeture douce : un arret force peut faire apparaitre un rapport d'erreur
    # Windows, qu'on prendrait a tort pour un defaut de l'application.
    if (-not $proc.HasExited) {
        $proc.CloseMainWindow() | Out-Null
        if (-not $proc.WaitForExit(5000)) { $proc.Kill() }
    }
}
