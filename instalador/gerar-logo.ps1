# Desenha o logo verde do Green Hell Companion e gera:
#   arte\logo.png (512), arte\logo.ico (16..256), arte\assistente*.bmp (painel lateral), arte\icone*.bmp (canto do assistente)
# Uso: powershell -ExecutionPolicy Bypass -File gerar-logo.ps1
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$arte = Join-Path $PSScriptRoot 'arte'
New-Item -ItemType Directory -Force $arte | Out-Null

function Cor($hex, $a = 255) {
    $c = [System.Drawing.ColorTranslator]::FromHtml($hex)
    [System.Drawing.Color]::FromArgb($a, $c.R, $c.G, $c.B)
}

function Novo-Grafico($bmp) {
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.PixelOffsetMode = 'HighQuality'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $g
}

function Caminho-Arredondado([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    $p
}

# Logo: quadrado arredondado verde-floresta, folha clara inclinada com nervuras e um losango âmbar (o marcador do mod).
function Desenhar-Logo($g, [float]$ox, [float]$oy, [float]$s, [bool]$fundo = $true) {
    if ($fundo) {
        $caixa = Caminho-Arredondado $ox $oy $s $s ($s * 0.22)
        $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            (New-Object System.Drawing.PointF($ox, $oy)), (New-Object System.Drawing.PointF(($ox + $s), ($oy + $s))),
            (Cor '#2f8a57'), (Cor '#123524'))
        $g.FillPath($grad, $caixa)
        $borda = New-Object System.Drawing.Pen((Cor '#7fd39f' 90), [Math]::Max(1, $s * 0.012))
        $g.DrawPath($borda, $caixa)
    }

    $estado = $g.Save()
    $g.TranslateTransform($ox + $s * 0.47, $oy + $s * 0.53)
    $g.RotateTransform(0)
    $L = $s * 0.66; $W = $s * 0.40

    # contorno da folha: largura = seno ao longo do eixo, base arredondada e ponta fina
    $pts = New-Object System.Collections.Generic.List[System.Drawing.PointF]
    $n = 48
    for ($i = 0; $i -le $n; $i++) {
        $t = $i / $n
        $meia = ($W / 2) * [Math]::Pow([Math]::Sin([Math]::PI * [Math]::Pow($t, 0.75)), 1.1)
        $pts.Add((New-Object System.Drawing.PointF($meia, ($L / 2 - $L * $t))))
    }
    for ($i = $n - 1; $i -ge 1; $i--) {
        $t = $i / $n
        $meia = ($W / 2) * [Math]::Pow([Math]::Sin([Math]::PI * [Math]::Pow($t, 0.75)), 1.1)
        $pts.Add((New-Object System.Drawing.PointF((-$meia), ($L / 2 - $L * $t))))
    }
    $folha = New-Object System.Drawing.Drawing2D.GraphicsPath
    $folha.AddPolygon($pts.ToArray())
    $g.FillPath((New-Object System.Drawing.SolidBrush((Cor '#e6f4dd'))), $folha)

    # nervura central e laterais
    $nerv = New-Object System.Drawing.Pen((Cor '#1d5c3b'), [Math]::Max(1, $s * 0.03))
    $nerv.StartCap = 'Round'; $nerv.EndCap = 'Round'
    $g.DrawLine($nerv, 0, ($L * 0.50), 0, (-$L * 0.36))
    $fina = New-Object System.Drawing.Pen((Cor '#1d5c3b'), [Math]::Max(1, $s * 0.018))
    $fina.StartCap = 'Round'; $fina.EndCap = 'Round'
    foreach ($v in @(-0.24, -0.04, 0.16)) {
        $y0 = $L * $v + $L * 0.06          # onde a nervura sai do eixo
        $y1 = $L * $v - $L * 0.06          # altura da ponta da nervura
        $tp = ($L / 2 - $y1) / $L          # posição ao longo da folha (0 = base, 1 = ponta)
        $x1 = 0.72 * ($W / 2) * [Math]::Pow([Math]::Sin([Math]::PI * [Math]::Pow($tp, 0.75)), 1.1)
        $g.DrawLine($fina, [float]0, [float]$y0, [float]$x1, [float]$y1)
        $g.DrawLine($fina, [float]0, [float]$y0, [float](-$x1), [float]$y1)
    }
    # cabo
    $g.DrawLine($nerv, 0, ($L * 0.50), 0, ($L * 0.62))
    $g.Restore($estado)

    # losango âmbar no canto superior direito
    $c = New-Object System.Drawing.PointF(($ox + $s * 0.76), ($oy + $s * 0.24))
    $r = $s * 0.10
    $pts = [System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF($c.X, ($c.Y - $r))),
        (New-Object System.Drawing.PointF(($c.X + $r), $c.Y)),
        (New-Object System.Drawing.PointF($c.X, ($c.Y + $r))),
        (New-Object System.Drawing.PointF(($c.X - $r), $c.Y)))
    $g.FillPolygon((New-Object System.Drawing.SolidBrush((Cor '#e8a93c'))), $pts)
}

function Salvar-Logo([int]$tam) {
    $bmp = New-Object System.Drawing.Bitmap($tam, $tam, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = Novo-Grafico $bmp
    $g.Clear([System.Drawing.Color]::Transparent)
    $m = [Math]::Max(0, [Math]::Round($tam * 0.02))
    Desenhar-Logo $g $m $m ($tam - 2 * $m)
    $g.Dispose()
    $bmp
}

# PNG grande
$png = Salvar-Logo 512
$png.Save((Join-Path $arte 'logo.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$png.Dispose()

# ICO com imagens PNG dentro (aceito desde o Windows Vista)
$tamanhos = 16, 24, 32, 48, 64, 128, 256
$blobs = foreach ($t in $tamanhos) {
    $b = Salvar-Logo $t
    $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $b.Dispose()
    , $ms.ToArray()
}
$ico = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter($ico)
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$tamanhos.Count)
$offset = 6 + 16 * $tamanhos.Count
for ($i = 0; $i -lt $tamanhos.Count; $i++) {
    $t = $tamanhos[$i]
    $w.Write([byte]($(if ($t -ge 256) { 0 } else { $t }))); $w.Write([byte]($(if ($t -ge 256) { 0 } else { $t })))
    $w.Write([byte]0); $w.Write([byte]0); $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$blobs[$i].Length); $w.Write([UInt32]$offset)
    $offset += $blobs[$i].Length
}
foreach ($bl in $blobs) { $w.Write($bl) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $arte 'logo.ico'), $ico.ToArray())

# Painel lateral do assistente (boas-vindas e fim): 164x314 a 100%, 328x628 a 200%
function Salvar-Lateral([int]$w, [int]$h, $nome) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = Novo-Grafico $bmp
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Point(0, 0)), (New-Object System.Drawing.Point(0, $h)), (Cor '#1f5a3a'), (Cor '#0b1f15'))
    $g.FillRectangle($grad, 0, 0, $w, $h)
    # folhagem sutil ao fundo
    $sombra = New-Object System.Drawing.SolidBrush((Cor '#2f8a57' 38))
    for ($i = 0; $i -lt 7; $i++) {
        $g.FillEllipse($sombra, ($w * (-0.3 + 0.25 * ($i % 4))), ($h * (0.55 + 0.07 * $i)), ($w * 0.9), ($h * 0.22))
    }
    $s = $w * 0.62
    Desenhar-Logo $g (($w - $s) / 2) ($h * 0.16) $s
    $f1 = New-Object System.Drawing.Font('Segoe UI Semibold', [float]($w * 0.105), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $f2 = New-Object System.Drawing.Font('Segoe UI', [float]($w * 0.085), [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $fmt = New-Object System.Drawing.StringFormat; $fmt.Alignment = 'Center'
    $g.DrawString('GREEN HELL', $f1, (New-Object System.Drawing.SolidBrush((Cor '#eef3ea'))), (New-Object System.Drawing.RectangleF(0, ($h * 0.16 + $s + $h * 0.04), $w, ($w * 0.2))), $fmt)
    $g.DrawString('Companion', $f2, (New-Object System.Drawing.SolidBrush((Cor '#e8a93c'))), (New-Object System.Drawing.RectangleF(0, ($h * 0.16 + $s + $h * 0.04 + $w * 0.13), $w, ($w * 0.2))), $fmt)
    $g.Dispose()
    $bmp.Save((Join-Path $arte $nome), [System.Drawing.Imaging.ImageFormat]::Bmp)
    $bmp.Dispose()
}
Salvar-Lateral 164 314 'assistente.bmp'
Salvar-Lateral 328 628 'assistente@2x.bmp'

# Ícone do canto superior das páginas internas: 55x55 e 110x110, fundo branco
function Salvar-Pequeno([int]$t, $nome) {
    $bmp = New-Object System.Drawing.Bitmap($t, $t, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = Novo-Grafico $bmp
    $g.Clear([System.Drawing.Color]::White)
    Desenhar-Logo $g 1 1 ($t - 2)
    $g.Dispose()
    $bmp.Save((Join-Path $arte $nome), [System.Drawing.Imaging.ImageFormat]::Bmp)
    $bmp.Dispose()
}
Salvar-Pequeno 55 'icone.bmp'
Salvar-Pequeno 110 'icone@2x.bmp'

Write-Host "Arte gerada em $arte"
