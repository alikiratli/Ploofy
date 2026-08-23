using SkiaSharp;

namespace Ploofy.Ui.Painting;

/// <summary>Tek bir parçacık.</summary>
public sealed class Particle
{
    public float X;
    public float Y;
    public float VelocityX;
    public float VelocityY;
    public float Radius;
    public float Spin;
    public float Rotation;
    public float Life;
    public float MaxLife;
    public SKColor Color;
    public bool IsSquare;
}

/// <summary>
/// Patlama ve kutlama parçacıklarını yürüten küçük motor.
/// </summary>
/// <remarks>
/// <para>
/// Bir balon "yok olarak" değil "dağılarak" kayboluyor. Aradaki fark küçük
/// görünüyor ama çocuk oyunlarında bütün tatmin hissi burada: dokunuşun bir
/// sonucu olduğunu gösteren şey parçacıklar.
/// </para>
/// <para>
/// Parçacıklar havuzdan geliyor, ölen parçacık listeden düşüyor; ayırma
/// yapılmadığı için oyun döngüsü sabit hızda kalıyor.
/// </para>
/// </remarks>
public sealed class ParticleField : IDisposable
{
    private readonly List<Particle> _particles = [];
    private readonly SKPaint _paint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    /// <summary>Aşağı çeken kuvvet. Konfeti düşsün, patlama parçaları yavaşlasın diye.</summary>
    public float Gravity { get; init; } = 900f;

    public bool IsEmpty => _particles.Count == 0;

    public int Count => _particles.Count;

    /// <summary>
    /// Bir noktada patlama üretir — patlayan balonun yerine.
    /// </summary>
    public void Burst(SKPoint center, float radius, HuePaint hue, Random rng, int count = 14)
    {
        for (var i = 0; i < count; i++)
        {
            // Eşit açıya küçük bir rastgelelik: tam simetrik patlama yapay duruyor.
            var angle = (MathF.Tau * i / count) + ((float)rng.NextDouble() * 0.4f);
            var speed = radius * (3.2f + ((float)rng.NextDouble() * 3.4f));

            _particles.Add(new Particle
            {
                X = center.X,
                Y = center.Y,
                VelocityX = MathF.Cos(angle) * speed,
                VelocityY = MathF.Sin(angle) * speed,
                Radius = radius * (0.08f + ((float)rng.NextDouble() * 0.16f)),
                Life = 0f,
                MaxLife = 0.45f + ((float)rng.NextDouble() * 0.35f),
                Color = i % 3 == 0 ? hue.Light : hue.Body,
                IsSquare = false,
                Spin = 0f,
            });
        }

        // Ortadan yayılan tek bir beyaz halka yerine birkaç parlak kıvılcım:
        // küçük ekranda halka bulanık, kıvılcım net görünüyor.
        for (var i = 0; i < 4; i++)
        {
            var angle = (float)rng.NextDouble() * MathF.Tau;
            _particles.Add(new Particle
            {
                X = center.X,
                Y = center.Y,
                VelocityX = MathF.Cos(angle) * radius * 5.5f,
                VelocityY = MathF.Sin(angle) * radius * 5.5f,
                Radius = radius * 0.09f,
                MaxLife = 0.3f,
                Color = SKColors.White,
            });
        }
    }

    /// <summary>
    /// Ekranın üstünden konfeti döker — tur bittiğinde.
    /// </summary>
    public void Confetti(float width, float height, Random rng, int count = 90)
    {
        for (var i = 0; i < count; i++)
        {
            var hue = PloofyPalette.All[rng.Next(PloofyPalette.All.Count)];

            _particles.Add(new Particle
            {
                X = (float)rng.NextDouble() * width,
                // Hepsi aynı anda değil, ekranın üstünde dağınık başlıyor:
                // konfeti tek bir çizgi hâlinde inerse perde gibi görünüyor.
                Y = -(float)rng.NextDouble() * height * 0.6f,
                VelocityX = ((float)rng.NextDouble() - 0.5f) * width * 0.25f,
                VelocityY = height * (0.15f + ((float)rng.NextDouble() * 0.25f)),
                Radius = width * (0.008f + ((float)rng.NextDouble() * 0.012f)),
                Rotation = (float)rng.NextDouble() * MathF.Tau,
                Spin = ((float)rng.NextDouble() - 0.5f) * 12f,
                MaxLife = 2.4f + ((float)rng.NextDouble() * 1.4f),
                Color = rng.Next(2) == 0 ? hue.Body : hue.Light,
                IsSquare = true,
            });
        }
    }

    public void Advance(float seconds)
    {
        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];

            p.Life += seconds;
            if (p.Life >= p.MaxLife)
            {
                _particles.RemoveAt(i);
                continue;
            }

            p.VelocityY += Gravity * seconds;
            p.X += p.VelocityX * seconds;
            p.Y += p.VelocityY * seconds;
            p.Rotation += p.Spin * seconds;

            // Sürtünme: parçacıklar ekranın dışına fırlamak yerine yavaşlayıp sönüyor.
            p.VelocityX *= 1f - MathF.Min(1f, 1.6f * seconds);
        }
    }

    public void Draw(SKCanvas canvas)
    {
        foreach (var p in _particles)
        {
            var remaining = 1f - (p.Life / p.MaxLife);
            _paint.Color = p.Color.WithAlpha((byte)(255 * MathF.Min(1f, remaining * 1.6f)));

            if (!p.IsSquare)
            {
                canvas.DrawCircle(p.X, p.Y, p.Radius * remaining, _paint);
                continue;
            }

            canvas.Save();
            canvas.Translate(p.X, p.Y);
            canvas.RotateRadians(p.Rotation);
            // Dönerken incelen dikdörtgen: kağıt parçasının çevrildiği izlenimi.
            var w = p.Radius * 2f;
            var h = p.Radius * 1.2f * MathF.Abs(MathF.Cos(p.Rotation));
            canvas.DrawRoundRect(-w / 2f, -h / 2f, w, h, p.Radius * 0.3f, p.Radius * 0.3f, _paint);
            canvas.Restore();
        }
    }

    public void Clear() => _particles.Clear();

    public void Dispose() => _paint.Dispose();
}
