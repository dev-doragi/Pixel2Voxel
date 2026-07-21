using System.Numerics;
using Silk.NET.OpenGL;

namespace PixelVoxel.Rendering;

/// <summary>Renders lit voxel pixels and screen-space outlines through OpenGL.</summary>
public sealed unsafe class SilkVoxelRenderer
{
    private const string DesktopGeometryVertexShader = """
        #version 330 core
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec4 aColor;
        layout(location = 2) in vec3 aNormal;
        layout(location = 3) in float aEditorMask;
        uniform mat4 uTransform;
        uniform mat4 uModelRotation;
        out vec4 vColor;
        flat out vec3 vNormal;
        flat out float vEditorMask;
        void main()
        {
            gl_Position = uTransform * vec4(aPosition, 1.0);
            vColor = aColor;
            vNormal = normalize(mat3(uModelRotation) * aNormal);
            vEditorMask = aEditorMask;
        }
        """;

    private const string DesktopGeometryFragmentShader = """
        #version 330 core
        in vec4 vColor;
        flat in vec3 vNormal;
        flat in float vEditorMask;
        layout(location = 0) out vec4 outputColor;
        layout(location = 1) out vec4 outputNormal;
        void main()
        {
            outputColor = vec4(vColor.rgb, 1.0);
            outputNormal = vec4((normalize(vNormal) * 0.5) + 0.5, vEditorMask);
        }
        """;

    private const string OpenGlesGeometryVertexShader = """
        #version 300 es
        precision highp float;
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec4 aColor;
        layout(location = 2) in vec3 aNormal;
        layout(location = 3) in float aEditorMask;
        uniform mat4 uTransform;
        uniform mat4 uModelRotation;
        out vec4 vColor;
        flat out vec3 vNormal;
        flat out float vEditorMask;
        void main()
        {
            gl_Position = uTransform * vec4(aPosition, 1.0);
            vColor = aColor;
            vNormal = normalize(mat3(uModelRotation) * aNormal);
            vEditorMask = aEditorMask;
        }
        """;

    private const string OpenGlesGeometryFragmentShader = """
        #version 300 es
        precision highp float;
        precision highp sampler2D;
        in vec4 vColor;
        flat in vec3 vNormal;
        flat in float vEditorMask;
        layout(location = 0) out vec4 outputColor;
        layout(location = 1) out vec4 outputNormal;
        void main()
        {
            outputColor = vec4(vColor.rgb, 1.0);
            outputNormal = vec4((normalize(vNormal) * 0.5) + 0.5, vEditorMask);
        }
        """;

    private const string DesktopPostVertexShader = """
        #version 330 core
        out vec2 vUv;
        void main()
        {
            vec2 positions[3] = vec2[3](
                vec2(-1.0, -1.0),
                vec2(3.0, -1.0),
                vec2(-1.0, 3.0));
            vec2 position = positions[gl_VertexID];
            gl_Position = vec4(position, 0.0, 1.0);
            vUv = (position + 1.0) * 0.5;
        }
        """;

    private const string OpenGlesPostVertexShader = """
        #version 300 es
        precision highp float;
        out vec2 vUv;
        void main()
        {
            vec2 positions[3] = vec2[3](
                vec2(-1.0, -1.0),
                vec2(3.0, -1.0),
                vec2(-1.0, 3.0));
            vec2 position = positions[gl_VertexID];
            gl_Position = vec4(position, 0.0, 1.0);
            vUv = (position + 1.0) * 0.5;
        }
        """;

    private const string DesktopPostFragmentShader = """
        #version 330 core
        uniform sampler2D uColorTexture;
        uniform sampler2D uNormalTexture;
        uniform sampler2D uDepthTexture;
        uniform int uLightingEnabled;
        uniform vec3 uLightDirection;
        uniform float uAmbient;
        uniform float uIntensity;
        uniform vec4 uBackgroundColor;
        uniform vec4 uOutlineColor;
        uniform int uOutlineEnabled;
        uniform int uOutlineMode;
        uniform float uDepthThreshold;
        out vec4 outputColor;

        bool inside(ivec2 p, ivec2 size) { return all(greaterThanEqual(p, ivec2(0))) && all(lessThan(p, size)); }
        bool covered(ivec2 p) { return texelFetch(uColorTexture, p, 0).a > 0.5; }

        void main()
        {
            ivec2 size = textureSize(uColorTexture, 0);
            ivec2 p = ivec2(gl_FragCoord.xy);
            vec4 center = texelFetch(uColorTexture, p, 0);
            bool centerCovered = center.a > 0.5;
            bool outline = false;

            if (uOutlineEnabled != 0 && !centerCovered)
            {
                for (int y = -1; y <= 1; y++)
                    for (int x = -1; x <= 1; x++)
                    {
                        ivec2 q = p + ivec2(x, y);
                        if ((x != 0 || y != 0) && inside(q, size) && covered(q)) outline = true;
                    }
            }
            else if (uOutlineEnabled != 0 && uOutlineMode == 1 && centerCovered)
            {
                ivec2 offsets[4] = ivec2[4](ivec2(-1,0), ivec2(1,0), ivec2(0,-1), ivec2(0,1));
                vec3 normal = (texelFetch(uNormalTexture, p, 0).xyz * 2.0) - 1.0;
                float depth = texelFetch(uDepthTexture, p, 0).r;
                for (int i = 0; i < 4; i++)
                {
                    ivec2 q = p + offsets[i];
                    if (!inside(q, size) || !covered(q)) continue;
                    vec3 otherNormal = (texelFetch(uNormalTexture, q, 0).xyz * 2.0) - 1.0;
                    float otherDepth = texelFetch(uDepthTexture, q, 0).r;
                    if (dot(normal, otherNormal) < 0.999 || abs(depth - otherDepth) > uDepthThreshold) outline = true;
                }
            }

            float editorMask = texelFetch(uNormalTexture, p, 0).a;
            bool editorOutline = false;
            if (centerCovered && editorMask > 0.1)
            {
                ivec2 editorOffsets[4] = ivec2[4](ivec2(-1,0), ivec2(1,0), ivec2(0,-1), ivec2(0,1));
                for (int i = 0; i < 4; i++)
                {
                    ivec2 q = p + editorOffsets[i];
                    if (!inside(q, size) || !covered(q) ||
                        abs(texelFetch(uNormalTexture, q, 0).a - editorMask) > 0.1) editorOutline = true;
                }
            }

            vec3 litColor = center.rgb;
            if (centerCovered && uLightingEnabled != 0)
            {
                vec3 normal = normalize((texelFetch(uNormalTexture, p, 0).xyz * 2.0) - 1.0);
                float diffuse = max(dot(normal, normalize(uLightDirection)), 0.0);
                float level = floor((diffuse * 3.0) + 0.5) / 3.0;
                float brightness = clamp(uAmbient + (uIntensity * level), 0.0, 1.0);
                litColor *= brightness;
            }
            vec4 editorColor = editorMask > 0.75 ? vec4(1.0, 0.835, 0.29, 1.0) : vec4(0.0, 0.843, 1.0, 1.0);
            outputColor = editorOutline ? editorColor : (outline ? uOutlineColor : (centerCovered ? vec4(litColor, 1.0) : uBackgroundColor));
        }
        """;

    private const string OpenGlesPostFragmentShader = """
        #version 300 es
        precision highp float;
        precision highp sampler2D;
        uniform sampler2D uColorTexture;
        uniform sampler2D uNormalTexture;
        uniform sampler2D uDepthTexture;
        uniform int uLightingEnabled;
        uniform vec3 uLightDirection;
        uniform float uAmbient;
        uniform float uIntensity;
        uniform vec4 uBackgroundColor;
        uniform vec4 uOutlineColor;
        uniform int uOutlineEnabled;
        uniform int uOutlineMode;
        uniform float uDepthThreshold;
        out vec4 outputColor;

        bool inside(ivec2 p, ivec2 size) { return all(greaterThanEqual(p, ivec2(0))) && all(lessThan(p, size)); }
        bool covered(ivec2 p) { return texelFetch(uColorTexture, p, 0).a > 0.5; }

        void main()
        {
            ivec2 size = textureSize(uColorTexture, 0);
            ivec2 p = ivec2(gl_FragCoord.xy);
            vec4 center = texelFetch(uColorTexture, p, 0);
            bool centerCovered = center.a > 0.5;
            bool outline = false;

            if (uOutlineEnabled != 0 && !centerCovered)
            {
                for (int y = -1; y <= 1; y++)
                    for (int x = -1; x <= 1; x++)
                    {
                        ivec2 q = p + ivec2(x, y);
                        if ((x != 0 || y != 0) && inside(q, size) && covered(q)) outline = true;
                    }
            }
            else if (uOutlineEnabled != 0 && uOutlineMode == 1 && centerCovered)
            {
                ivec2 offsets[4] = ivec2[4](ivec2(-1,0), ivec2(1,0), ivec2(0,-1), ivec2(0,1));
                vec3 normal = (texelFetch(uNormalTexture, p, 0).xyz * 2.0) - 1.0;
                float depth = texelFetch(uDepthTexture, p, 0).r;
                for (int i = 0; i < 4; i++)
                {
                    ivec2 q = p + offsets[i];
                    if (!inside(q, size) || !covered(q)) continue;
                    vec3 otherNormal = (texelFetch(uNormalTexture, q, 0).xyz * 2.0) - 1.0;
                    float otherDepth = texelFetch(uDepthTexture, q, 0).r;
                    if (dot(normal, otherNormal) < 0.999 || abs(depth - otherDepth) > uDepthThreshold) outline = true;
                }
            }

            float editorMask = texelFetch(uNormalTexture, p, 0).a;
            bool editorOutline = false;
            if (centerCovered && editorMask > 0.1)
            {
                ivec2 editorOffsets[4] = ivec2[4](ivec2(-1,0), ivec2(1,0), ivec2(0,-1), ivec2(0,1));
                for (int i = 0; i < 4; i++)
                {
                    ivec2 q = p + editorOffsets[i];
                    if (!inside(q, size) || !covered(q) ||
                        abs(texelFetch(uNormalTexture, q, 0).a - editorMask) > 0.1) editorOutline = true;
                }
            }

            vec3 litColor = center.rgb;
            if (centerCovered && uLightingEnabled != 0)
            {
                vec3 normal = normalize((texelFetch(uNormalTexture, p, 0).xyz * 2.0) - 1.0);
                float diffuse = max(dot(normal, normalize(uLightDirection)), 0.0);
                float level = floor((diffuse * 3.0) + 0.5) / 3.0;
                float brightness = clamp(uAmbient + (uIntensity * level), 0.0, 1.0);
                litColor *= brightness;
            }
            vec4 editorColor = editorMask > 0.75 ? vec4(1.0, 0.835, 0.29, 1.0) : vec4(0.0, 0.843, 1.0, 1.0);
            outputColor = editorOutline ? editorColor : (outline ? uOutlineColor : (centerCovered ? vec4(litColor, 1.0) : uBackgroundColor));
        }
        """;

    private GL? _gl;
    private VoxelMeshData? _pendingMesh;
    private bool _meshDirty;
    private uint _geometryProgram;
    private uint _postProgram;
    private uint _meshVertexArray;
    private uint _postVertexArray;
    private uint _vertexBuffer;
    private uint _indexBuffer;
    private uint _geometryFramebuffer;
    private uint _geometryColorTexture;
    private uint _normalTexture;
    private uint _depthTexture;
    private uint _outputFramebuffer;
    private uint _outputTexture;
    private int _transformLocation;
    private int _modelRotationLocation;
    private int _lightingEnabledLocation;
    private int _lightDirectionLocation;
    private int _ambientLocation;
    private int _intensityLocation;
    private int _backgroundLocation;
    private int _outlineColorLocation;
    private int _outlineEnabledLocation;
    private int _outlineModeLocation;
    private int _depthThresholdLocation;
    private int _colorSamplerLocation;
    private int _normalSamplerLocation;
    private int _depthSamplerLocation;
    private int _indexCount;
    private int _framebufferWidth;
    private int _framebufferHeight;
    private bool _isOpenGles;

    /// <summary>Gets whether OpenGL resources have been initialized.</summary>
    public bool IsInitialized => _gl is not null;

    /// <summary>Initializes both geometry and post-process shader programs.</summary>
    public void Initialize(Func<string, nint> getProcAddress, bool isOpenGles)
    {
        ArgumentNullException.ThrowIfNull(getProcAddress);
        if (_gl is not null)
        {
            throw new InvalidOperationException("The OpenGL renderer is already initialized.");
        }

        _gl = GL.GetApi(getProcAddress);
        _isOpenGles = isOpenGles;
        _geometryProgram = CreateProgram(
            _gl,
            isOpenGles ? OpenGlesGeometryVertexShader : DesktopGeometryVertexShader,
            isOpenGles ? OpenGlesGeometryFragmentShader : DesktopGeometryFragmentShader);
        _postProgram = CreateProgram(
            _gl,
            isOpenGles ? OpenGlesPostVertexShader : DesktopPostVertexShader,
            isOpenGles ? OpenGlesPostFragmentShader : DesktopPostFragmentShader);
        _meshVertexArray = _gl.GenVertexArray();
        _postVertexArray = _gl.GenVertexArray();
        _vertexBuffer = _gl.GenBuffer();
        _indexBuffer = _gl.GenBuffer();
        ResolveUniforms(_gl);
    }

    /// <summary>Queues immutable mesh data for upload in the next GL callback.</summary>
    public void SetMesh(VoxelMeshData? mesh)
    {
        _pendingMesh = mesh;
        _meshDirty = true;
    }

    /// <summary>Renders geometry, post-processes the logical pixels, and presents at an integer scale.</summary>
    public void Render(
        int targetFramebuffer,
        int targetWidth,
        int targetHeight,
        VoxelRenderTransform transform,
        PixelRenderLayout layout,
        VoxelRenderStyle style,
        int? manualScale)
    {
        GL gl = GetGl();
        ArgumentNullException.ThrowIfNull(transform);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(style);
        if (targetWidth <= 0 || targetHeight <= 0)
        {
            return;
        }

        UploadPendingMesh(gl);
        EnsureFramebuffers(gl, layout.Width, layout.Height);
        ConfigurePixelState(gl);
        RenderGeometry(gl, transform, layout, style);
        RenderPostProcess(gl, layout, style);
        Present(gl, checked((uint)targetFramebuffer), targetWidth, targetHeight, layout, style, manualScale);
    }

    /// <summary>Deletes all GL resources while the owning context is current.</summary>
    public void Deinitialize()
    {
        if (_gl is not GL gl)
        {
            return;
        }

        DeleteFramebuffers(gl);
        if (_indexBuffer != 0) gl.DeleteBuffer(_indexBuffer);
        if (_vertexBuffer != 0) gl.DeleteBuffer(_vertexBuffer);
        if (_postVertexArray != 0) gl.DeleteVertexArray(_postVertexArray);
        if (_meshVertexArray != 0) gl.DeleteVertexArray(_meshVertexArray);
        if (_postProgram != 0) gl.DeleteProgram(_postProgram);
        if (_geometryProgram != 0) gl.DeleteProgram(_geometryProgram);
        gl.Dispose();
        _gl = null;
        _pendingMesh = null;
    }

    private void RenderGeometry(
        GL gl,
        VoxelRenderTransform transform,
        PixelRenderLayout layout,
        VoxelRenderStyle style)
    {
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _geometryFramebuffer);
        GLEnum* attachments = stackalloc GLEnum[2]
        {
            GLEnum.ColorAttachment0,
            GLEnum.ColorAttachment1,
        };
        gl.DrawBuffers(2, attachments);
        gl.Viewport(0, 0, (uint)layout.Width, (uint)layout.Height);
        gl.ClearColor(0f, 0f, 0f, 0f);
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        gl.Enable(EnableCap.DepthTest);
        gl.Enable(EnableCap.CullFace);
        gl.CullFace(TriangleFace.Back);
        gl.FrontFace(FrontFaceDirection.Ccw);
        gl.DepthFunc(DepthFunction.Less);

        if (_indexCount == 0)
        {
            return;
        }

        gl.UseProgram(_geometryProgram);
        SetMatrix(gl, _transformLocation, transform.ClipFromModel);
        SetMatrix(gl, _modelRotationLocation, transform.ModelRotation);
        gl.BindVertexArray(_meshVertexArray);
        gl.DrawElements(
            PrimitiveType.Triangles,
            (uint)_indexCount,
            DrawElementsType.UnsignedInt,
            null);
    }

    private void RenderPostProcess(GL gl, PixelRenderLayout layout, VoxelRenderStyle style)
    {
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _outputFramebuffer);
        GLEnum attachment = GLEnum.ColorAttachment0;
        gl.DrawBuffers(1, &attachment);
        gl.Viewport(0, 0, (uint)layout.Width, (uint)layout.Height);
        gl.Disable(EnableCap.DepthTest);
        gl.Disable(EnableCap.CullFace);
        gl.UseProgram(_postProgram);
        BindTexture(gl, TextureUnit.Texture0, _geometryColorTexture, 0, _colorSamplerLocation);
        BindTexture(gl, TextureUnit.Texture1, _normalTexture, 1, _normalSamplerLocation);
        BindTexture(gl, TextureUnit.Texture2, _depthTexture, 2, _depthSamplerLocation);
        gl.Uniform1(_lightingEnabledLocation, style.Lighting.Enabled ? 1 : 0);
        gl.Uniform3(_lightDirectionLocation, style.Lighting.Direction);
        gl.Uniform1(_ambientLocation, Math.Clamp(style.Lighting.Ambient, 0f, 1f));
        gl.Uniform1(_intensityLocation, Math.Clamp(style.Lighting.Intensity, 0f, 1f));
        gl.Uniform4(_backgroundLocation, ToVector4(style.Background));
        gl.Uniform4(_outlineColorLocation, ToVector4(style.Outline.Color));
        gl.Uniform1(_outlineEnabledLocation, style.Outline.Enabled ? 1 : 0);
        gl.Uniform1(
            _outlineModeLocation,
            style.Outline.Mode == VoxelOutlineMode.SilhouetteAndDepth ? 1 : 0);
        gl.Uniform1(
            _depthThresholdLocation,
            Math.Max(0f, style.Outline.DepthThreshold) * 0.5f / layout.ModelDiagonal);
        gl.BindVertexArray(_postVertexArray);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
    }

    private void Present(
        GL gl,
        uint targetFramebuffer,
        int targetWidth,
        int targetHeight,
        PixelRenderLayout layout,
        VoxelRenderStyle style,
        int? manualScale)
    {
        gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, targetFramebuffer);
        gl.Viewport(0, 0, (uint)targetWidth, (uint)targetHeight);
        Vector4 background = ToVector4(style.Background);
        gl.ClearColor(background.X, background.Y, background.Z, background.W);
        gl.Clear(ClearBufferMask.ColorBufferBit);

        PixelViewportMapping mapping = PixelViewportMapping.Create(
            targetWidth,
            targetHeight,
            layout.Width,
            layout.Height,
            manualScale);
        int destinationWidth = layout.Width * mapping.Scale;
        int destinationHeight = layout.Height * mapping.Scale;

        gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _outputFramebuffer);
        gl.BlitFramebuffer(
            0,
            0,
            layout.Width,
            layout.Height,
            mapping.DestinationX,
            mapping.DestinationY,
            mapping.DestinationX + destinationWidth,
            mapping.DestinationY + destinationHeight,
            ClearBufferMask.ColorBufferBit,
            BlitFramebufferFilter.Nearest);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, targetFramebuffer);
    }

    private void UploadPendingMesh(GL gl)
    {
        if (!_meshDirty)
        {
            return;
        }

        _meshDirty = false;
        if (_pendingMesh is not VoxelMeshData mesh)
        {
            _indexCount = 0;
            return;
        }

        _pendingMesh = null;
        ReadOnlySpan<VoxelMeshVertex> vertices = mesh.Vertices.Span;
        float[] interleaved = new float[checked(vertices.Length * 11)];
        for (int index = 0; index < vertices.Length; index++)
        {
            VoxelMeshVertex vertex = vertices[index];
            int offset = index * 11;
            interleaved[offset] = vertex.Position.X;
            interleaved[offset + 1] = vertex.Position.Y;
            interleaved[offset + 2] = vertex.Position.Z;
            interleaved[offset + 3] = vertex.Color.Red / 255f;
            interleaved[offset + 4] = vertex.Color.Green / 255f;
            interleaved[offset + 5] = vertex.Color.Blue / 255f;
            interleaved[offset + 6] = vertex.Color.Alpha / 255f;
            interleaved[offset + 7] = vertex.Normal.X;
            interleaved[offset + 8] = vertex.Normal.Y;
            interleaved[offset + 9] = vertex.Normal.Z;
            interleaved[offset + 10] = vertex.EditorMask;
        }

        uint[] indices = mesh.Indices.ToArray();
        gl.BindVertexArray(_meshVertexArray);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
        fixed (float* vertexPointer = interleaved)
        {
            gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                checked((nuint)(interleaved.Length * sizeof(float))),
                vertexPointer,
                BufferUsageARB.StaticDraw);
        }

        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _indexBuffer);
        fixed (uint* indexPointer = indices)
        {
            gl.BufferData(
                BufferTargetARB.ElementArrayBuffer,
                checked((nuint)(indices.Length * sizeof(uint))),
                indexPointer,
                BufferUsageARB.StaticDraw);
        }

        const uint stride = 11 * sizeof(float);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, null);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, stride, (void*)(7 * sizeof(float)));
        gl.EnableVertexAttribArray(3);
        gl.VertexAttribPointer(3, 1, VertexAttribPointerType.Float, false, stride, (void*)(10 * sizeof(float)));
        _indexCount = indices.Length;
    }

    private void EnsureFramebuffers(GL gl, int width, int height)
    {
        if (_geometryFramebuffer != 0 && _framebufferWidth == width && _framebufferHeight == height)
        {
            return;
        }

        DeleteFramebuffers(gl);
        _framebufferWidth = width;
        _framebufferHeight = height;

        _geometryFramebuffer = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _geometryFramebuffer);
        _geometryColorTexture = CreateColorTexture(gl, width, height);
        _normalTexture = CreateColorTexture(gl, width, height);
        _depthTexture = CreateDepthTexture(gl, width, height);
        AttachTexture(gl, FramebufferAttachment.ColorAttachment0, _geometryColorTexture);
        AttachTexture(gl, FramebufferAttachment.ColorAttachment1, _normalTexture);
        AttachTexture(gl, FramebufferAttachment.DepthAttachment, _depthTexture);
        EnsureComplete(gl, "geometry");

        _outputFramebuffer = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _outputFramebuffer);
        _outputTexture = CreateColorTexture(gl, width, height);
        AttachTexture(gl, FramebufferAttachment.ColorAttachment0, _outputTexture);
        EnsureComplete(gl, "output");
    }

    private static uint CreateColorTexture(GL gl, int width, int height)
    {
        uint texture = gl.GenTexture();
        ConfigureTexture(gl, texture);
        gl.TexImage2D(
            TextureTarget.Texture2D,
            0,
            InternalFormat.Rgba8,
            (uint)width,
            (uint)height,
            0,
            Silk.NET.OpenGL.PixelFormat.Rgba,
            PixelType.UnsignedByte,
            null);
        return texture;
    }

    private static uint CreateDepthTexture(GL gl, int width, int height)
    {
        uint texture = gl.GenTexture();
        ConfigureTexture(gl, texture);
        gl.TexImage2D(
            TextureTarget.Texture2D,
            0,
            InternalFormat.DepthComponent24,
            (uint)width,
            (uint)height,
            0,
            Silk.NET.OpenGL.PixelFormat.DepthComponent,
            PixelType.UnsignedInt,
            null);
        return texture;
    }

    private static void ConfigureTexture(GL gl, uint texture)
    {
        gl.BindTexture(TextureTarget.Texture2D, texture);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
    }

    private static void AttachTexture(GL gl, FramebufferAttachment attachment, uint texture) =>
        gl.FramebufferTexture2D(
            FramebufferTarget.Framebuffer,
            attachment,
            TextureTarget.Texture2D,
            texture,
            0);

    private static void EnsureComplete(GL gl, string name)
    {
        GLEnum status = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
        {
            throw new InvalidOperationException($"The {name} pixel framebuffer is incomplete: {status}.");
        }
    }

    private void DeleteFramebuffers(GL gl)
    {
        if (_outputTexture != 0) gl.DeleteTexture(_outputTexture);
        if (_depthTexture != 0) gl.DeleteTexture(_depthTexture);
        if (_normalTexture != 0) gl.DeleteTexture(_normalTexture);
        if (_geometryColorTexture != 0) gl.DeleteTexture(_geometryColorTexture);
        if (_outputFramebuffer != 0) gl.DeleteFramebuffer(_outputFramebuffer);
        if (_geometryFramebuffer != 0) gl.DeleteFramebuffer(_geometryFramebuffer);
        _outputTexture = 0;
        _depthTexture = 0;
        _normalTexture = 0;
        _geometryColorTexture = 0;
        _outputFramebuffer = 0;
        _geometryFramebuffer = 0;
        _framebufferWidth = 0;
        _framebufferHeight = 0;
    }

    private void ConfigurePixelState(GL gl)
    {
        gl.Disable(EnableCap.Blend);
        gl.Disable(EnableCap.Multisample);
        if (!_isOpenGles)
        {
            gl.Disable(EnableCap.FramebufferSrgb);
        }
    }

    private void ResolveUniforms(GL gl)
    {
        _transformLocation = RequireUniform(gl, _geometryProgram, "uTransform");
        _modelRotationLocation = RequireUniform(gl, _geometryProgram, "uModelRotation");
        _lightingEnabledLocation = RequireUniform(gl, _postProgram, "uLightingEnabled");
        _lightDirectionLocation = RequireUniform(gl, _postProgram, "uLightDirection");
        _ambientLocation = RequireUniform(gl, _postProgram, "uAmbient");
        _intensityLocation = RequireUniform(gl, _postProgram, "uIntensity");
        _backgroundLocation = RequireUniform(gl, _postProgram, "uBackgroundColor");
        _outlineColorLocation = RequireUniform(gl, _postProgram, "uOutlineColor");
        _outlineEnabledLocation = RequireUniform(gl, _postProgram, "uOutlineEnabled");
        _outlineModeLocation = RequireUniform(gl, _postProgram, "uOutlineMode");
        _depthThresholdLocation = RequireUniform(gl, _postProgram, "uDepthThreshold");
        _colorSamplerLocation = RequireUniform(gl, _postProgram, "uColorTexture");
        _normalSamplerLocation = RequireUniform(gl, _postProgram, "uNormalTexture");
        _depthSamplerLocation = RequireUniform(gl, _postProgram, "uDepthTexture");
    }

    private static int RequireUniform(GL gl, uint program, string name)
    {
        int location = gl.GetUniformLocation(program, name);
        return location >= 0
            ? location
            : throw new InvalidOperationException($"The OpenGL uniform {name} was not found.");
    }

    private static void BindTexture(
        GL gl,
        TextureUnit unit,
        uint texture,
        int samplerIndex,
        int samplerLocation)
    {
        gl.ActiveTexture(unit);
        gl.BindTexture(TextureTarget.Texture2D, texture);
        gl.Uniform1(samplerLocation, samplerIndex);
    }

    private static void SetMatrix(GL gl, int location, Matrix4x4 matrix)
    {
        float* values = stackalloc float[16]
        {
            matrix.M11, matrix.M12, matrix.M13, matrix.M14,
            matrix.M21, matrix.M22, matrix.M23, matrix.M24,
            matrix.M31, matrix.M32, matrix.M33, matrix.M34,
            matrix.M41, matrix.M42, matrix.M43, matrix.M44,
        };
        gl.UniformMatrix4(location, 1, false, values);
    }

    private static Vector4 ToVector4(PixelVoxel.Core.Rgba32Color color) =>
        new(color.Red / 255f, color.Green / 255f, color.Blue / 255f, color.Alpha / 255f);

    private static uint CreateProgram(GL gl, string vertexSource, string fragmentSource)
    {
        uint vertexShader = CompileShader(gl, ShaderType.VertexShader, vertexSource);
        uint fragmentShader = CompileShader(gl, ShaderType.FragmentShader, fragmentSource);
        uint program = gl.CreateProgram();
        try
        {
            gl.AttachShader(program, vertexShader);
            gl.AttachShader(program, fragmentShader);
            gl.LinkProgram(program);
            gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int linked);
            if (linked == 0)
            {
                throw new InvalidOperationException($"OpenGL program link failed: {gl.GetProgramInfoLog(program)}");
            }

            return program;
        }
        catch
        {
            gl.DeleteProgram(program);
            throw;
        }
        finally
        {
            gl.DetachShader(program, vertexShader);
            gl.DetachShader(program, fragmentShader);
            gl.DeleteShader(vertexShader);
            gl.DeleteShader(fragmentShader);
        }
    }

    private static uint CompileShader(GL gl, ShaderType type, string source)
    {
        uint shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);
        gl.GetShader(shader, ShaderParameterName.CompileStatus, out int compiled);
        if (compiled != 0)
        {
            return shader;
        }

        string message = gl.GetShaderInfoLog(shader);
        gl.DeleteShader(shader);
        throw new InvalidOperationException($"OpenGL {type} compilation failed: {message}");
    }

    private GL GetGl() =>
        _gl ?? throw new InvalidOperationException("The OpenGL renderer is not initialized.");
}
