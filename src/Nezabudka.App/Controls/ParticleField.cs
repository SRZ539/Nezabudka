using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Pen = System.Windows.Media.Pen;

namespace Nezabudka.App.Controls;

public sealed class ParticleField : FrameworkElement
{
    private const int ParticleCount = 22;
    private static readonly TimeSpan MinimumFrameInterval = TimeSpan.FromSeconds(1.0 / 30.0);
    private readonly List<Particle> _particles = new(ParticleCount);
    private Brush? _drawingBrush;
    private Pen? _connectionPen;
    private Pen? _shapePen;
    private TimeSpan? _lastFrameTime;
    private bool _isRendering;

    public ParticleField()
    {
        IsHitTestVisible = false;
        SnapsToDevicePixels = true;

        var random = new Random(401);
        for (var index = 0; index < ParticleCount; index++)
        {
            var shapeRoll = random.NextDouble();
            _particles.Add(new Particle
            {
                X = random.NextDouble(),
                Y = random.NextDouble(),
                VelocityX = (random.NextDouble() - 0.5) * 0.018,
                VelocityY = -0.006 - (random.NextDouble() * 0.016),
                Radius = 1.1 + (random.NextDouble() * 2.2),
                BaseOpacity = 0.16 + (random.NextDouble() * 0.28),
                PulsePhase = random.NextDouble() * Math.PI * 2,
                PulseSpeed = 0.7 + (random.NextDouble() * 1.4),
                WobblePhase = random.NextDouble() * Math.PI * 2,
                WobbleSpeed = 0.35 + random.NextDouble(),
                Rotation = random.NextDouble() * Math.PI * 2,
                RotationSpeed = (random.NextDouble() - 0.5) * 0.7,
                Shape = shapeRoll switch
                {
                    < 0.43 => ParticleShape.Dot,
                    < 0.64 => ParticleShape.Ring,
                    < 0.79 => ParticleShape.Diamond,
                    < 0.95 => ParticleShape.Spark,
                    _ => ParticleShape.Flower
                }
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
        EnsureDrawingResources(accentBrush);
        const double connectionDistance = 138;

        for (var first = 0; first < _particles.Count; first++)
        {
            if (_particles[first].Shape is not (ParticleShape.Dot or ParticleShape.Ring))
            {
                continue;
            }

            var firstPoint = ToPoint(_particles[first]);
            for (var second = first + 1; second < _particles.Count; second++)
            {
                if (_particles[second].Shape is not (ParticleShape.Dot or ParticleShape.Ring))
                {
                    continue;
                }

                var secondPoint = ToPoint(_particles[second]);
                var distance = (firstPoint - secondPoint).Length;
                if (distance >= connectionDistance)
                {
                    continue;
                }

                drawingContext.PushOpacity((1 - (distance / connectionDistance)) * 0.11);
                drawingContext.DrawLine(_connectionPen, firstPoint, secondPoint);
                drawingContext.Pop();
            }
        }

        foreach (var particle in _particles)
        {
            var pulse = 0.72 + (Math.Sin(particle.PulsePhase) * 0.28);
            drawingContext.PushOpacity(particle.BaseOpacity * pulse);
            DrawParticle(drawingContext, particle, accentBrush);
            drawingContext.Pop();
        }
    }

    private System.Windows.Point ToPoint(Particle particle)
    {
        var wobble = Math.Sin(particle.WobblePhase) * 0.004;
        return new((particle.X + wobble) * ActualWidth, particle.Y * ActualHeight);
    }

    private void EnsureDrawingResources(Brush brush)
    {
        if (ReferenceEquals(_drawingBrush, brush))
        {
            return;
        }

        _drawingBrush = brush;
        _connectionPen = new Pen(brush, 0.65);
        _shapePen = new Pen(brush, 1.15)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        if (_connectionPen.CanFreeze)
        {
            _connectionPen.Freeze();
        }

        if (_shapePen.CanFreeze)
        {
            _shapePen.Freeze();
        }
    }

    private void DrawParticle(DrawingContext context, Particle particle, Brush brush)
    {
        var center = ToPoint(particle);
        var radius = particle.Radius;
        switch (particle.Shape)
        {
            case ParticleShape.Dot:
                context.DrawEllipse(brush, null, center, radius, radius);
                break;
            case ParticleShape.Ring:
                context.DrawEllipse(null, _shapePen, center, radius * 1.45, radius * 1.45);
                break;
            case ParticleShape.Diamond:
                var diamondRadius = radius * 1.8;
                var top = RotatePoint(center, diamondRadius, particle.Rotation);
                var right = RotatePoint(center, diamondRadius, particle.Rotation + (Math.PI / 2));
                var bottom = RotatePoint(center, diamondRadius, particle.Rotation + Math.PI);
                var left = RotatePoint(center, diamondRadius, particle.Rotation + (Math.PI * 1.5));
                context.DrawLine(_shapePen, top, right);
                context.DrawLine(_shapePen, right, bottom);
                context.DrawLine(_shapePen, bottom, left);
                context.DrawLine(_shapePen, left, top);
                break;
            case ParticleShape.Spark:
                var sparkRadius = radius * 2.5;
                context.DrawLine(
                    _shapePen,
                    RotatePoint(center, sparkRadius, particle.Rotation),
                    RotatePoint(center, sparkRadius, particle.Rotation + Math.PI));
                context.DrawLine(
                    _shapePen,
                    RotatePoint(center, sparkRadius * 0.45, particle.Rotation + (Math.PI / 2)),
                    RotatePoint(center, sparkRadius * 0.45, particle.Rotation + (Math.PI * 1.5)));
                break;
            case ParticleShape.Flower:
                var petalDistance = radius * 1.5;
                for (var petal = 0; petal < 5; petal++)
                {
                    var angle = particle.Rotation + (petal * Math.PI * 2 / 5);
                    var petalCenter = RotatePoint(center, petalDistance, angle);
                    context.DrawEllipse(brush, null, petalCenter, radius * 0.72, radius * 0.72);
                }

                context.DrawEllipse(brush, null, center, radius * 0.52, radius * 0.52);
                break;
        }
    }

    private static System.Windows.Point RotatePoint(System.Windows.Point center, double radius, double angle) =>
        new(center.X + (Math.Cos(angle) * radius), center.Y + (Math.Sin(angle) * radius));

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
            particle.WobblePhase += particle.WobbleSpeed * elapsed;
            particle.Rotation += particle.RotationSpeed * elapsed;
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

        public double WobblePhase { get; set; }

        public double WobbleSpeed { get; init; }

        public double Rotation { get; set; }

        public double RotationSpeed { get; init; }

        public ParticleShape Shape { get; init; }
    }

    private enum ParticleShape
    {
        Dot,
        Ring,
        Diamond,
        Spark,
        Flower
    }
}
