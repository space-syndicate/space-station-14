using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.Utility;
using Robust.Shared.ContentPack;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client.Corvax.ExportSprites;

public sealed class EntityScreenshotRenderService
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private readonly IResourceManager _resourceManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private EntityScreenshotRenderControl? _control;
    private bool _initialized;
    private readonly Dictionary<(ResPath Path, string State), Image<Rgba32>> _rsiStateImageCache = new();
    private readonly Dictionary<Texture, Image<Rgba32>> _textureImageCache = new();
    private ISawmill _sawmill = default!;

    public void Initialize()
    {
        if (_initialized)
            return;

        IoCManager.InjectDependencies(this);
        _sawmill = _logManager.GetSawmill("corvax.entity-sprite-export");
        _initialized = true;
    }

    public void Shutdown()
    {
        foreach (var image in _rsiStateImageCache.Values)
        {
            image.Dispose();
        }

        _rsiStateImageCache.Clear();

        foreach (var image in _textureImageCache.Values)
        {
            image.Dispose();
        }

        _textureImageCache.Clear();

        if (_control == null)
            return;

        foreach (var queued in _control.QueuedTextures)
        {
            queued.Tcs.SetCanceled();
        }

        _control.QueuedTextures.Clear();
        _ui.RootControl.RemoveChild(_control);
        _control = null;
    }

    public async Task Export(EntityUid entity,
        Direction direction,
        ResPath outputPath,
        CancellationToken cancelToken = default)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (!_entityManager.TryGetComponent<SpriteComponent>(entity, out var spriteComp))
            return;

        var renderBounds = GetRenderBounds(spriteComp);

        if (renderBounds.Size.Equals(Vector2i.Zero))
            return;

        var animationLayers = GetAnimatedLayers(spriteComp);
        if (animationLayers.Count == 0)
        {
            DeleteIfExists(GetAnimationDirectory(outputPath));
            await ExportFrame(entity, direction, outputPath, renderBounds, cancelToken);
            return;
        }

        var animationFrames = BuildAnimationFrames(entity, spriteComp, animationLayers);
        if (animationFrames.Count <= 1)
        {
            DeleteIfExists(GetAnimationDirectory(outputPath));
            await ExportFrame(entity, direction, outputPath, renderBounds, cancelToken);
            return;
        }

        await ExportAnimation(entity, direction, outputPath, renderBounds, spriteComp, animationLayers, animationFrames, cancelToken);
    }

    private void EnsureControlAttached()
    {
        if (!_initialized)
            Initialize();

        if (_control != null)
            return;

        _control = new EntityScreenshotRenderControl();
        _ui.RootControl.AddChild(_control);
    }

    private async Task ExportAnimation(
        EntityUid entity,
        Direction direction,
        ResPath outputPath,
        SpriteRenderBounds renderBounds,
        SpriteComponent spriteComp,
        IReadOnlyList<AnimatedLayerInfo> animationLayers,
        IReadOnlyList<AnimationFrameInfo> animationFrames,
        CancellationToken cancelToken)
    {
        var originalTimes = new float[animationLayers.Count];
        for (var i = 0; i < animationLayers.Count; i++)
        {
            originalTimes[i] = spriteComp[animationLayers[i].Index].AnimationTime;
        }

        var animationDir = GetAnimationDirectory(outputPath);
        DeleteIfExists(outputPath);
        DeleteIfExists(animationDir);
        _resourceManager.UserData.CreateDir(animationDir);

        try
        {
            foreach (var t in animationFrames)
            {
                cancelToken.ThrowIfCancellationRequested();
                ApplyAnimationTime(entity, spriteComp, animationLayers, t.RenderTimeSeconds);
                var framePath = animationDir / t.FileName;
                await ExportFrame(entity, direction, framePath, renderBounds, cancelToken);
            }

            WriteAnimationMetadata(animationDir, animationFrames);
        }
        finally
        {
            for (var i = 0; i < animationLayers.Count; i++)
            {
                _entitySystemManager.GetEntitySystem<SpriteSystem>()
                    .LayerSetAnimationTime((entity, spriteComp), animationLayers[i].Index, originalTimes[i]);
            }
        }
    }

    private async Task ExportFrame(
        EntityUid entity,
        Direction direction,
        ResPath outputPath,
        SpriteRenderBounds renderBounds,
        CancellationToken cancelToken)
    {
        if (TryExportFrameDirect(entity, direction, outputPath, renderBounds))
            return;

        EnsureControlAttached();

        var texture = _clyde.CreateRenderTarget(
            renderBounds.Size,
            new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
            name: "corvax-entity-export");

        var tcs = new TaskCompletionSource(cancelToken);
        _control!.QueuedTextures.Enqueue((texture, direction, entity, outputPath, tcs));
        await tcs.Task;
    }

    private static SpriteRenderBounds GetRenderBounds(SpriteComponent spriteComp)
    {
        var hasVisibleLayers = false;
        var min = Vector2i.Zero;
        var max = Vector2i.Zero;

        foreach (var layer in spriteComp.AllLayers)
        {
            if (layer is not SpriteComponent.Layer spriteLayer || !spriteLayer.Visible)
                continue;

            var pixelOffset = ToPixelOffset(spriteComp.Offset + spriteLayer.Offset);
            var halfSize = spriteLayer.PixelSize / 2;
            var topLeft = pixelOffset - halfSize;
            var bottomRight = topLeft + spriteLayer.PixelSize;

            if (!hasVisibleLayers)
            {
                min = topLeft;
                max = bottomRight;
                hasVisibleLayers = true;
                continue;
            }

            min = Vector2i.ComponentMin(min, topLeft);
            max = Vector2i.ComponentMax(max, bottomRight);
        }

        return !hasVisibleLayers
            ? new SpriteRenderBounds(Vector2i.Zero, Vector2i.Zero)
            : new SpriteRenderBounds(min, max - min);
    }

    private static ResPath GetAnimationDirectory(ResPath outputPath)
    {
        return outputPath.Directory / "_animated" / outputPath.FilenameWithoutExtension;
    }

    private void DeleteIfExists(ResPath path)
    {
        if (_resourceManager.UserData.Exists(path))
            _resourceManager.UserData.Delete(path);
    }

    private static List<AnimatedLayerInfo> GetAnimatedLayers(SpriteComponent spriteComp)
    {
        var result = new List<AnimatedLayerInfo>();
        var index = 0;

        foreach (var spriteLayer in spriteComp.AllLayers)
        {
            if (!spriteLayer.Visible ||
                !spriteLayer.AutoAnimated ||
                spriteLayer.ActualRsi == null ||
                !spriteLayer.ActualRsi.TryGetState(spriteLayer.RsiState, out var state) ||
                !state.IsAnimated ||
                state.TotalDelay <= 0f)
            {
                index++;
                continue;
            }

            result.Add(new AnimatedLayerInfo(index, state.TotalDelay, state.GetDelays()));
            index++;
        }

        return result;
    }

    private List<AnimationFrameInfo> BuildAnimationFrames(
        EntityUid entity,
        SpriteComponent spriteComp,
        IReadOnlyList<AnimatedLayerInfo> animationLayers)
    {
        const float epsilon = 0.0001f;
        const int maxFrames = 512;

        var initialSignature = BuildAnimationSignatureAt(entity, spriteComp, animationLayers, 0f, epsilon);
        var frames = new List<AnimationFrameInfo>();
        var currentFrameStart = 0f;

        for (var i = 0; i < maxFrames; i++)
        {
            var nextDelta = GetNextBoundaryDelta(currentFrameStart, animationLayers, epsilon);
            if (nextDelta <= epsilon)
                break;

            var renderTime = currentFrameStart + MathF.Min(epsilon, nextDelta * 0.5f);
            frames.Add(new AnimationFrameInfo($"{i:D4}.png", renderTime, ToDelayMilliseconds(nextDelta)));

            var nextFrameStart = currentFrameStart + nextDelta;
            if (BuildAnimationSignatureAt(entity, spriteComp, animationLayers, nextFrameStart, epsilon) == initialSignature)
                break;

            currentFrameStart = nextFrameStart;
        }

        ApplyAnimationTime(entity, spriteComp, animationLayers, 0f);
        return frames;
    }

    private void ApplyAnimationTime(
        EntityUid entity,
        SpriteComponent spriteComp,
        IReadOnlyList<AnimatedLayerInfo> animationLayers,
        float timeSeconds)
    {
        var spriteSystem = _entitySystemManager.GetEntitySystem<SpriteSystem>();

        foreach (var layer in animationLayers)
        {
            spriteSystem.LayerSetAnimationTime((entity, spriteComp), layer.Index, timeSeconds);
        }
    }

    private static string BuildAnimationSignature(
        SpriteComponent spriteComp,
        IReadOnlyList<AnimatedLayerInfo> animationLayers)
    {
        var parts = new string[animationLayers.Count];

        for (var i = 0; i < animationLayers.Count; i++)
        {
            var layer = spriteComp[animationLayers[i].Index];
            parts[i] = $"{animationLayers[i].Index}:{layer.Visible}:{layer.RsiState.Name}:{layer.AnimationFrame}";
        }

        return string.Join("|", parts);
    }

    private string BuildAnimationSignatureAt(
        EntityUid entity,
        SpriteComponent spriteComp,
        IReadOnlyList<AnimatedLayerInfo> animationLayers,
        float frameStartTime,
        float epsilon)
    {
        var nextDelta = GetNextBoundaryDelta(frameStartTime, animationLayers, epsilon);
        var probeTime = nextDelta > epsilon
            ? frameStartTime + MathF.Min(epsilon, nextDelta * 0.5f)
            : frameStartTime;

        ApplyAnimationTime(entity, spriteComp, animationLayers, probeTime);
        return BuildAnimationSignature(spriteComp, animationLayers);
    }

    private static float GetNextBoundaryDelta(
        float currentTime,
        IReadOnlyList<AnimatedLayerInfo> animationLayers,
        float epsilon)
    {
        var nextDelta = float.MaxValue;
        var foundDelta = false;

        foreach (var layer in animationLayers)
        {
            if (layer.TotalDelay <= epsilon)
                continue;

            var mod = currentTime % layer.TotalDelay;
            var cumulative = 0f;
            float? layerDelta = null;

            foreach (var delay in layer.Delays)
            {
                cumulative += delay;
                if (cumulative > mod + epsilon)
                {
                    layerDelta = cumulative - mod;
                    break;
                }
            }

            layerDelta ??= layer.TotalDelay - mod;

            if (layerDelta.Value > epsilon && layerDelta.Value < nextDelta)
            {
                nextDelta = layerDelta.Value;
                foundDelta = true;
            }
        }

        return foundDelta ? nextDelta : 0f;
    }

    private static int ToDelayMilliseconds(float seconds)
    {
        return Math.Max(1, (int) MathF.Round(seconds * 1000f));
    }

    private void WriteAnimationMetadata(ResPath animationDir, IReadOnlyList<AnimationFrameInfo> animationFrames)
    {
        using var writer = _resourceManager.UserData.OpenWriteText(animationDir / "frames.txt");
        foreach (var frame in animationFrames)
        {
            writer.WriteLine($"{frame.FileName}\t{frame.DelayMilliseconds}");
        }

        writer.Flush();
    }

    private readonly record struct AnimatedLayerInfo(int Index, float TotalDelay, float[] Delays);
    private readonly record struct AnimationFrameInfo(string FileName, float RenderTimeSeconds, int DelayMilliseconds);
    private readonly record struct SpriteRenderBounds(Vector2i Min, Vector2i Size);

    private bool TryExportFrameDirect(
        EntityUid entity,
        Direction direction,
        ResPath outputPath,
        SpriteRenderBounds renderBounds)
    {
        if (!_entityManager.TryGetComponent<SpriteComponent>(entity, out var spriteComp))
            return false;

        // Keep the old render-target path for uncommon transformed sprites.
        if (spriteComp.Scale != Vector2.One ||
            spriteComp.Rotation != Angle.Zero ||
            spriteComp.EnableDirectionOverride)
            return false;

        var size = renderBounds.Size;
        if (size == Vector2i.Zero)
            return true;

        using var image = new Image<Rgba32>(size.X, size.Y);
        var buffer = image.GetPixelSpan();

        foreach (var baseLayer in spriteComp.AllLayers)
        {
            if (baseLayer is not SpriteComponent.Layer spriteLayer || !spriteLayer.Visible)
                continue;

            if (spriteLayer.Scale != Vector2.One || spriteLayer.Rotation != Angle.Zero)
                return false;

            if (!TryGetLayerImage(spriteLayer, direction, out var sourceImage, out var sourceRect))
                return false;

            var drawColor = spriteComp.Color * spriteLayer.Color;
            var drawOffset = ToPixelOffset(spriteComp.Offset + spriteLayer.Offset) - renderBounds.Min;
            var topLeft = drawOffset - new Vector2i(sourceRect.Width, sourceRect.Height) / 2;
            BlitImage(sourceImage, sourceRect, drawColor, buffer, size, topLeft);
        }

        if (!_resourceManager.UserData.IsDir(outputPath.Directory))
            _resourceManager.UserData.CreateDir(outputPath.Directory);

        if (_resourceManager.UserData.Exists(outputPath))
            _resourceManager.UserData.Delete(outputPath);

        using var file = _resourceManager.UserData.OpenWrite(outputPath);
        image.SaveAsPng(file);
        _sawmill.Info($"Saved screenshot to {outputPath} (direct)");
        return true;
    }

    private static void BlitImage(
        Image<Rgba32> sourceImage,
        PixelRect sourceRect,
        Color modulation,
        Span<Rgba32> destination,
        Vector2i destinationSize,
        Vector2i topLeft)
    {
        var source = sourceImage.GetPixelSpan();
        var sourceWidth = sourceImage.Width;

        for (var y = 0; y < sourceRect.Height; y++)
        {
            var dstY = topLeft.Y + y;
            if (dstY < 0 || dstY >= destinationSize.Y)
                continue;

            var srcY = sourceRect.Top + y;
            for (var x = 0; x < sourceRect.Width; x++)
            {
                var dstX = topLeft.X + x;
                if (dstX < 0 || dstX >= destinationSize.X)
                    continue;

                var srcX = sourceRect.Left + x;
                var texel = source[srcY * sourceWidth + srcX];
                var src = Modulate(texel, modulation);
                if (src.A == 0)
                    continue;

                ref var dst = ref destination[dstY * destinationSize.X + dstX];
                BlendPixel(ref dst, src);
            }
        }
    }

    private static Rgba32 Modulate(Rgba32 texel, Color modulation)
    {
        return new Rgba32(
            (byte) (texel.R * modulation.RByte / byte.MaxValue),
            (byte) (texel.G * modulation.GByte / byte.MaxValue),
            (byte) (texel.B * modulation.BByte / byte.MaxValue),
            (byte) (texel.A * modulation.AByte / byte.MaxValue));
    }

    private static void BlendPixel(ref Rgba32 destination, Rgba32 source)
    {
        if (source.A == byte.MaxValue)
        {
            destination = source;
            return;
        }

        var srcAlpha = source.A / 255f;
        var dstAlpha = destination.A / 255f;
        var outAlpha = srcAlpha + dstAlpha * (1f - srcAlpha);

        if (outAlpha <= 0f)
        {
            destination = default;
            return;
        }

        var srcR = source.R / 255f;
        var srcG = source.G / 255f;
        var srcB = source.B / 255f;
        var dstR = destination.R / 255f;
        var dstG = destination.G / 255f;
        var dstB = destination.B / 255f;

        var outR = (srcR * srcAlpha + dstR * dstAlpha * (1f - srcAlpha)) / outAlpha;
        var outG = (srcG * srcAlpha + dstG * dstAlpha * (1f - srcAlpha)) / outAlpha;
        var outB = (srcB * srcAlpha + dstB * dstAlpha * (1f - srcAlpha)) / outAlpha;

        destination = new Rgba32(
            (byte) Math.Clamp((int) MathF.Round(outR * 255f), 0, 255),
            (byte) Math.Clamp((int) MathF.Round(outG * 255f), 0, 255),
            (byte) Math.Clamp((int) MathF.Round(outB * 255f), 0, 255),
            (byte) Math.Clamp((int) MathF.Round(outAlpha * 255f), 0, 255));
    }

    private static Vector2i ToPixelOffset(Vector2 offset)
    {
        return new Vector2i(
            (int) MathF.Round(offset.X * EyeManager.PixelsPerMeter),
            (int) MathF.Round(offset.Y * EyeManager.PixelsPerMeter));
    }

    private bool TryGetLayerImage(
        SpriteComponent.Layer layer,
        Direction direction,
        out Image<Rgba32> image,
        out PixelRect sourceRect)
    {
        image = default!;
        sourceRect = default;

        if (layer.Texture != null)
        {
            image = GetTextureImage(layer.Texture);
            sourceRect = new PixelRect(0, 0, image.Width, image.Height);
            return true;
        }

        var rsi = layer.ActualRsi;
        var stateId = ((ISpriteLayer) layer).RsiState;
        if (rsi == null ||
            !stateId.IsValid ||
            !rsi.TryGetState(stateId, out var state))
        {
            return false;
        }

        var rsiPath = rsi.Path;
        var stateName = stateId.Name!;

        if (!_rsiStateImageCache.TryGetValue((rsiPath, stateName), out image!))
        {
            using var stream = _resourceManager.ContentFileRead(rsiPath / (stateName + ".png"));
            image = Image.Load<Rgba32>(stream);
            _rsiStateImageCache[(rsiPath, stateName)] = image;
        }

        var frameWidth = rsi.Size.X;
        var frameHeight = rsi.Size.Y;
        var statesX = image.Width / frameWidth;
        var statesY = image.Height / frameHeight;
        var totalFrames = statesX * statesY;
        var dirCount = state.RsiDirections switch
        {
            Robust.Shared.Graphics.RSI.RsiDirectionType.Dir1 => 1,
            Robust.Shared.Graphics.RSI.RsiDirectionType.Dir4 => 4,
            Robust.Shared.Graphics.RSI.RsiDirectionType.Dir8 => 8,
            _ => 1
        };

        if (totalFrames == 0 || totalFrames % dirCount != 0)
            return false;

        var framesPerDirection = totalFrames / dirCount;
        var frame = Math.Clamp(layer.AnimationFrame, 0, framesPerDirection - 1);
        var rsiDirection = direction.Convert(state.RsiDirections).OffsetRsiDir(layer.DirOffset);
        var target = (int) rsiDirection * framesPerDirection + frame;
        var targetY = target / statesX;
        var targetX = target % statesX;
        sourceRect = new PixelRect(targetX * frameWidth, targetY * frameHeight, frameWidth, frameHeight);
        return true;
    }

    private Image<Rgba32> GetTextureImage(Texture texture)
    {
        if (_textureImageCache.TryGetValue(texture, out var cached))
            return cached;

        var image = new Image<Rgba32>(texture.Width, texture.Height);
        var pixels = image.GetPixelSpan();

        for (var y = 0; y < texture.Height; y++)
        {
            for (var x = 0; x < texture.Width; x++)
            {
                var color = texture.GetPixel(x, y);
                pixels[y * texture.Width + x] = new Rgba32(color.RByte, color.GByte, color.BByte, color.AByte);
            }
        }

        _textureImageCache[texture] = image;
        return image;
    }

    private readonly record struct PixelRect(int Left, int Top, int Width, int Height);

    private sealed class EntityScreenshotRenderControl : Control
    {
        private static readonly Color ExportBackgroundColor = new(128, 128, 128, 0);

        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly ILogManager _logManager = default!;
        [Dependency] private readonly IResourceManager _resourceManager = default!;

        internal readonly Queue<(
            IRenderTexture Texture,
            Direction Direction,
            EntityUid Entity,
            ResPath OutputPath,
            TaskCompletionSource Tcs)> QueuedTextures = new();

        private readonly ISawmill _sawmill;

        public EntityScreenshotRenderControl()
        {
            IoCManager.InjectDependencies(this);
            _sawmill = _logManager.GetSawmill("corvax.entity-sprite-export");
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);

            while (QueuedTextures.TryDequeue(out var queued))
            {
                if (queued.Tcs.Task.IsCanceled)
                    continue;

                try
                {
                    if (!_entityManager.EntityExists(queued.Entity))
                    {
                        queued.Texture.Dispose();
                        queued.Tcs.SetResult();
                        continue;
                    }

                    var result = queued;
                    handle.RenderInRenderTarget(queued.Texture,
                        () =>
                        {
                            handle.DrawEntity(result.Entity,
                                result.Texture.Size / 2,
                                Vector2.One,
                                Angle.Zero,
                                overrideDirection: result.Direction);
                        },
                        ExportBackgroundColor);

                    if (!_resourceManager.UserData.IsDir(queued.OutputPath.Directory))
                        _resourceManager.UserData.CreateDir(queued.OutputPath.Directory);

                    var result1 = queued;
                    queued.Texture.CopyPixelsToMemory<Rgba32>(image =>
                    {
                        try
                        {
                            if (_resourceManager.UserData.Exists(result.OutputPath))
                            {
                                _sawmill.Info($"Found existing file {result.OutputPath} to replace.");
                                _resourceManager.UserData.Delete(result.OutputPath);
                            }

                            using var file = _resourceManager.UserData.OpenWrite(result.OutputPath);
                            image.SaveAsPng(file);
                            _sawmill.Info($"Saved screenshot to {result.OutputPath}");
                            result1.Tcs.SetResult();
                        }
                        catch (Exception exc)
                        {
                            if (!string.IsNullOrEmpty(exc.StackTrace))
                                _sawmill.Fatal(exc.StackTrace);

                            result1.Tcs.SetException(exc);
                        }
                        finally
                        {
                            image.Dispose();
                            result1.Texture.Dispose();
                        }
                    });
                }
                catch (Exception exc)
                {
                    queued.Texture.Dispose();

                    if (!string.IsNullOrEmpty(exc.StackTrace))
                        _sawmill.Fatal(exc.StackTrace);

                    queued.Tcs.SetException(exc);
                }
            }
        }
    }
}
