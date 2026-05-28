using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Lazybones.Features.Shell;

// A short-lived particle burst rendered on top of the main disk. Triggered
// when a streak completes — small rotating rectangles spawn from the centre,
// fall under gravity with a subtle sine-wobble (the thing that sells "paper
// confetti" rather than "rain"), fade out near the end of their lifetime,
// then the burst stops on its own.
public class ConfettiBurst : Control
{
    private const int ParticleCount = 40;
    private const double LifetimeSeconds = 2.0;
    private const double Gravity = 480.0;        // px/s²
    private const double WobbleAmplitude = 60.0; // px peak horizontal sway
    private const double WobbleFrequency = 2.0;  // wobble cycles per second
    private const double InitialOutwardSpeed = 90.0;
    private const double InitialUpwardSpeed = 200.0;
    private const double FadeBeginRatio = 0.7;   // fraction of lifetime before fade starts

    private static readonly Color[] Palette =
    [
        Color.FromRgb(0xFF, 0x4C, 0x4C), // red
        Color.FromRgb(0xFF, 0x9B, 0x3D), // orange
        Color.FromRgb(0xFF, 0xD1, 0x66), // yellow
        Color.FromRgb(0x66, 0xBB, 0x6A), // green
        Color.FromRgb(0x4C, 0xC2, 0xFF), // blue
        Color.FromRgb(0xAB, 0x7D, 0xF6), // purple
        Color.FromRgb(0xFF, 0x8A, 0xC4), // pink
    ];

    private sealed class Particle
    {
        public double X;
        public double Y;
        public double Vx;
        public double Vy;
        public double Rotation;
        public double SpinRate;
        public double Age;
        public double WobblePhase;
        public Color Color = Colors.White;
    }

    private readonly List<Particle> _particles = new();
    private DispatcherTimer? _timer;
    private DateTime _lastTick;
    private readonly Random _random = new();

    public ConfettiBurst()
    {
        IsHitTestVisible = false;
    }

    public void Burst()
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0) return;

        var centerX = Bounds.Width / 2;
        var centerY = Bounds.Height / 2;

        for (var i = 0; i < ParticleCount; i++)
        {
            var angle = _random.NextDouble() * Math.PI * 2;
            var speed = InitialOutwardSpeed * (0.4 + _random.NextDouble() * 1.2);

            _particles.Add(new Particle
            {
                X = centerX,
                Y = centerY,
                Vx = Math.Cos(angle) * speed,
                Vy = Math.Sin(angle) * speed - InitialUpwardSpeed * (0.4 + _random.NextDouble() * 0.8),
                Rotation = _random.NextDouble() * Math.PI * 2,
                SpinRate = (_random.NextDouble() * 12 - 6),
                WobblePhase = _random.NextDouble() * Math.PI * 2,
                Color = Palette[_random.Next(Palette.Length)],
                Age = 0
            });
        }

        if (_timer is null)
        {
            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnFrame);
            _lastTick = DateTime.UtcNow;
            _timer.Start();
        }
        InvalidateVisual();
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var dt = (now - _lastTick).TotalSeconds;
        if (dt > 0.1) dt = 0.1; // avoid huge dt after a stall
        _lastTick = now;

        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Age += dt;
            if (p.Age >= LifetimeSeconds)
            {
                _particles.RemoveAt(i);
                continue;
            }

            // Wobble = a horizontal sine added on top of base velocity. Frequency
            // and phase are per-particle so the field doesn't sway in unison.
            var wobble = Math.Cos((p.Age * WobbleFrequency + p.WobblePhase) * Math.PI * 2) * WobbleAmplitude * dt;
            p.X += p.Vx * dt + wobble;
            p.Y += p.Vy * dt;
            p.Vy += Gravity * dt;
            p.Rotation += p.SpinRate * dt;
        }

        InvalidateVisual();

        if (_particles.Count == 0)
        {
            _timer?.Stop();
            _timer = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        if (_particles.Count == 0) return;

        foreach (var p in _particles)
        {
            var lifeRatio = p.Age / LifetimeSeconds;
            var alpha = lifeRatio >= FadeBeginRatio
                ? 1.0 - (lifeRatio - FadeBeginRatio) / (1.0 - FadeBeginRatio)
                : 1.0;
            if (alpha <= 0) continue;

            var color = Color.FromArgb((byte)(alpha * 255), p.Color.R, p.Color.G, p.Color.B);
            var brush = new SolidColorBrush(color);

            using var _ = context.PushTransform(
                Matrix.CreateRotation(p.Rotation) *
                Matrix.CreateTranslation(p.X, p.Y));

            context.FillRectangle(brush, new Rect(-2, -4, 4, 8));
        }
    }
}
