# Genere l'icone de FoundMySave : une loupe sur un fichier de sauvegarde.
# Produit un .ico multi-resolutions et un .png pour le depot.
# Usage : pwsh tools/make-icon.ps1

Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$assets = Join-Path $root 'src\FoundMySave\Assets'
New-Item -ItemType Directory -Force $assets | Out-Null

# Palette identique a celle de l'interface.
$bg      = [System.Drawing.ColorTranslator]::FromHtml('#1B1E27')
$edge    = [System.Drawing.ColorTranslator]::FromHtml('#2C3040')
$accent  = [System.Drawing.ColorTranslator]::FromHtml('#C8953F')
$paper   = [System.Drawing.ColorTranslator]::FromHtml('#E8EAF0')
$paperDim= [System.Drawing.ColorTranslator]::FromHtml('#9AA1B4')

function New-Logo([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

    $s = $size / 256.0   # tout est dessine sur une grille de 256, puis mis a l'echelle

    # Fond : carre aux coins arrondis
    $r = 48 * $s
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, 2*$r, 2*$r, 180, 90)
    $path.AddArc($size - 2*$r, 0, 2*$r, 2*$r, 270, 90)
    $path.AddArc($size - 2*$r, $size - 2*$r, 2*$r, 2*$r, 0, 90)
    $path.AddArc(0, $size - 2*$r, 2*$r, 2*$r, 90, 90)
    $path.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush($bg)), $path)
    $g.DrawPath((New-Object System.Drawing.Pen($edge, [Math]::Max(1, 3*$s))), $path)

    # Le document : une feuille au coin replie.
    # Chaque coordonnee est calculee a part : dans New-Object Type($a + $b, $c),
    # PowerShell lit la virgule avant l'addition et fabrique un tableau.
    $dx = 58 * $s; $dy = 46 * $s; $dw = 104 * $s; $dh = 132 * $s; $fold = 30 * $s
    $xLeft   = [single]$dx
    $xRight  = [single]($dx + $dw)
    $xFold   = [single]($dx + $dw - $fold)
    $yTop    = [single]$dy
    $yFold   = [single]($dy + $fold)
    $yBottom = [single]($dy + $dh)

    $doc = New-Object System.Drawing.Drawing2D.GraphicsPath
    $doc.AddLines([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new($xLeft,  $yTop),
        [System.Drawing.PointF]::new($xFold,  $yTop),
        [System.Drawing.PointF]::new($xRight, $yFold),
        [System.Drawing.PointF]::new($xRight, $yBottom),
        [System.Drawing.PointF]::new($xLeft,  $yBottom)
    ))
    $doc.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush($paper)), $doc)

    # Le coin replie, plus sombre
    $corner = New-Object System.Drawing.Drawing2D.GraphicsPath
    $corner.AddLines([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new($xFold,  $yTop),
        [System.Drawing.PointF]::new($xRight, $yFold),
        [System.Drawing.PointF]::new($xFold,  $yFold)
    ))
    $corner.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush($paperDim)), $corner)

    # Quelques lignes de texte suggerees
    $lineBrush = New-Object System.Drawing.SolidBrush($paperDim)
    for ($i = 0; $i -lt 3; $i++) {
        $ly = $dy + (58 * $s) + ($i * 22 * $s)
        $g.FillRectangle($lineBrush, $dx + 16*$s, $ly, $dw - 42*$s, 8*$s)
    }

    # La loupe, en bas a droite, qui deborde du document
    $cx = 158 * $s; $cy = 152 * $s; $rad = 52 * $s
    $ring = [Math]::Max(2, 14 * $s)

    # Le verre eclaircit ce qu'il couvre au lieu de l'assombrir, sinon la partie du
    # document situee dessous vire au brun et le dessin perd en lisibilite.
    $glass = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(56, 255, 255, 255))
    $g.FillEllipse($glass, $cx - $rad, $cy - $rad, 2*$rad, 2*$rad)

    # Manche, dessine avant l'anneau pour passer dessous
    $handle = New-Object System.Drawing.Pen($accent, [Math]::Max(2, 20 * $s))
    $handle.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $handle.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $hx = $cx + ($rad * 0.72); $hy = $cy + ($rad * 0.72)
    $g.DrawLine($handle, $hx, $hy, $hx + 44*$s, $hy + 44*$s)

    $g.DrawEllipse((New-Object System.Drawing.Pen($accent, $ring)), $cx - $rad, $cy - $rad, 2*$rad, 2*$rad)

    $g.Dispose()
    return $bmp
}

# Un .ico contient plusieurs tailles : Windows choisit la bonne selon le contexte.
$sizes = @(256, 128, 64, 48, 32, 16)
$images = @{}
foreach ($size in $sizes) {
    $bmp = New-Logo $size
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $images[$size] = $ms.ToArray()
    if ($size -eq 256) { $bmp.Save((Join-Path $assets 'logo.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose()
    $ms.Dispose()
}

# Ecriture du conteneur ICO : en-tete, puis une entree par image, puis les donnees.
$icoPath = Join-Path $assets 'icon.ico'
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)

$bw.Write([UInt16]0)               # reserve
$bw.Write([UInt16]1)               # type 1 = icone
$bw.Write([UInt16]$sizes.Count)

$offset = 6 + (16 * $sizes.Count)
foreach ($size in $sizes) {
    $data = $images[$size]
    # 0 signifie 256 dans le format ICO, qui code la taille sur un seul octet.
    $bw.Write([Byte]$(if ($size -ge 256) { 0 } else { $size }))
    $bw.Write([Byte]$(if ($size -ge 256) { 0 } else { $size }))
    $bw.Write([Byte]0)             # palette
    $bw.Write([Byte]0)             # reserve
    $bw.Write([UInt16]1)           # plans
    $bw.Write([UInt16]32)          # bits par pixel
    $bw.Write([UInt32]$data.Length)
    $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($size in $sizes) { $bw.Write($images[$size]) }

$bw.Flush(); $bw.Close(); $fs.Close()

Write-Host "icon.ico : $((Get-Item $icoPath).Length) octets, $($sizes.Count) resolutions"
Write-Host "logo.png : $((Get-Item (Join-Path $assets 'logo.png')).Length) octets"
