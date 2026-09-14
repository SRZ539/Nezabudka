using System.Windows;
using System.Windows.Media;

namespace Nezabudka.App.Controls;

public sealed class ParticleField : FrameworkElement
{
    private const int ParticleCount = 20;
    private static readonly TimeSpan MinimumFrameInterval = TimeSpan.FromSeconds(1.0 / 30.0);
    private readonly List<Particle> _particles = new(ParticleCount);
    private TimeSpan? _lastFrameTime;
    private bool _isRendering;

    public ParticleField()
    {
        IsHitTestVisible = false;
        SnapsToDevicePixels = true;

        var random = new Random(401);
        for (var index = 0; index < ParticleCount; index++)
        {
            _particles.Add(new Particle
            {
                X = random.NextDouble(),
                Y = random.NextDouble(),
                VelocityX = (random.NextDouble() - 0.5) * 0.018,
                VelocityY = -0.006 - (random.NextDouble() * 0.016),
                Radius = 1.1 + (random.NextDouble() * 2.2),
                BaseOpacity = 0.16 + (random.NextDouble() * 0.28),
                PulsePhase = random.NextDouble() * Math.PI * 2,
                PulseSpeed = 0.7 + (random.NextDouble() * 1.4)
            });
        }

        Loaded += (_, _) => UpdateRenderingSubscription();
        Unloaded += (_, _) => StopRendering();
        IsVisibleChanged += (_, _) => UpdateRenderingSubscription();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var accentBrush = TryFindResource("AccentBrush") as System.Windows.Media.Brush
                          ?? System.Windows.Media.Brushes.White;
        var connectionPen = new System.Windows.Media.Pen(accentBrush, 0.65);
        const double connectionDistance = 138;

        for (var first = 0; first < _particles.Count; first++)
        {
            var firstPoint = ToPoint(_particles[first]);
            for (var second = first + 1; second < _particles.Count; second++)
            {
                var secondPoint = ToPoint(_particles[second]);
                var distance = (firstPoint - secondPoint).Length;
                if (distance >= connectionDistance)
                {
                    continue;
                }

                drawingContext.PushOpacity((1 - (distance / connectionDistance)) * 0.11);
                drawingContext.DrawLine(connectionPen, firstPoint, secondPoint);
                drawingContext.Pop();
            }
        }

        foreach (var particle in _particles)
        {
            var pulse = 0.72 + (Math.Sin(particle.PulsePhase) * 0.28);
            drawingContext.PushOpacity(particle.BaseOpacity * pulse);
            drawingContext.DrawEllipse(
                accentBrush,
                null,
                ToPoint(particle),
                particle.Radius,
                particle.Radius);
            drawingContext.Pop();
        }
    }

    private System.Windows.Point ToPoint(Particle particle) =>
        new(particle.X * ActualWidth, particle.Y * ActualHeight);

    private void UpdateRenderingSubscription()
    {
        if (IsLoaded && IsVisible)
        {
            StartRendering();
        }
        else
        {
            StopRendering();
        }
    }

    private void StartRendering()
    {
        if (_isRendering)
        {
            return;
        }

        _lastFrameTime = null;
        CompositionTarget.Rendering += OnRendering;
        _isRendering = true;
    }

    private void StopRendering()
    {
        if (!_isRendering)
        {
            return;
        }

        CompositionTarget.Rendering -= OnRendering;
        _lastFrameTime = null;
        _isRendering = false;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs renderingEvent)
        {
            return;
        }

        if (_lastFrameTime is null)
        {
            _lastFrameTime = renderingEvent.RenderingTime;
            return;
        }

        var frameInterval = renderingEvent.RenderingTime - _lastFrameTime.Value;
        if (frameInterval < MinimumFrameInterval)
        {
            return;
        }

        var elapsed = Math.Clamp(
            frameInterval.TotalSeconds,
            0,
            0.05);
        _lastFrameTime = renderingEvent.RenderingTime;

        foreach (var particle in _particles)
        {
            particle.X = Wrap(particle.X + (particle.VelocityX * elapsed));
            particle.Y = Wrap(particle.Y + (particle.VelocityY * elapsed));
            particle.PulsePhase += particle.PulseSpeed * elapsed;
        }

        InvalidateVisual();
    }

    private static double Wrap(double value)
    {
        if (value < 0)
        {
            return value + 1;
        }

        return value >= 1 ? value - 1 : value;
    }

    private sealed class Particle
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double VelocityX { get; init; }

        public double VelocityY { get; init; }

        public double Radius { get; init; }

        public double BaseOpacity { get; init; }

        public double PulsePhase { get; set; }

        public double PulseSpeed { get; init; }
    }
}
