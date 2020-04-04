﻿//
//  RenderableWorldModel.cs
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) 2017 Jarl Gullberg
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using System;
using System.Collections.Generic;
using System.Linq;
using Everlook.Configuration;
using Everlook.Exceptions.Shader;
using Everlook.Utility;
using Everlook.Viewport.Camera;
using Everlook.Viewport.Rendering.Core;
using Everlook.Viewport.Rendering.Interfaces;
using Everlook.Viewport.Rendering.Shaders;
using log4net;
using OpenTK;
using Warcraft.WMO;
using Warcraft.WMO.GroupFile;
using Warcraft.WMO.GroupFile.Chunks;
using Warcraft.WMO.RootFile.Chunks;
using BufferTarget = OpenTK.Graphics.OpenGL.BufferTarget;
using BufferUsageHint = OpenTK.Graphics.OpenGL.BufferUsageHint;
using DrawElementsType = OpenTK.Graphics.OpenGL.DrawElementsType;
using EnableCap = OpenTK.Graphics.OpenGL.EnableCap;
using GL = OpenTK.Graphics.OpenGL.GL;
using PrimitiveType = OpenTK.Graphics.OpenGL.PrimitiveType;
using TextureUnit = OpenTK.Graphics.OpenGL.TextureUnit;
using TextureWrapMode = OpenTK.Graphics.OpenGL.TextureWrapMode;
using VertexAttribPointerType = OpenTK.Graphics.OpenGL.VertexAttribPointerType;

namespace Everlook.Viewport.Rendering
{
    /// <summary>
    /// Represents a renderable World Model Object.
    /// </summary>
    public sealed class RenderableWorldModel : IRenderable, ITickingActor, IDefaultCameraPositionProvider, IModelInfoProvider
    {
        /// <summary>
        /// Logger instance for this class.
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(typeof(RenderableWorldModel));

        /// <summary>
        /// Gets or sets a value indicating whether this object has been disposed.
        /// </summary>
        private bool IsDisposed { get; set; }

        /// <inheritdoc />
        public bool IsStatic => false;

        /// <inheritdoc />
        public ProjectionType Projection => ProjectionType.Perspective;

        /// <inheritdoc />
        public Vector3 DefaultCameraPosition
        {
            get
            {
                if (!this.IsInitialized)
                {
                    return Vector3.Zero;
                }

                if (this._model.Groups.Count == 0)
                {
                    return Vector3.Zero;
                }

                return
                (
                    this.ActorTransform.GetModelMatrix() *
                    new Vector4
                    (
                        this._model.Groups
                        .First()
                        .GetBoundingBox()
                        .GetCenterCoordinates()
                        .ToOpenGLVector(),
                        1.0f
                    )
                )
                .Xyz;
            }
        }

        /// <summary>
        /// The model contained by this renderable world object.
        /// </summary>
        /// <value>The model.</value>
        private readonly WMO _model;

        /// <inheritdoc />
        public Transform ActorTransform { get; set; }

        private readonly RenderCache _cache = RenderCache.Instance;
        private readonly WarcraftGameContext _gameContext;

        /// <summary>
        /// Dictionary that maps texture paths to OpenGL textures.
        /// </summary>
        private readonly Dictionary<string, Texture2D> _textureLookup = new Dictionary<string, Texture2D>();

        // Actual model data
        private readonly Dictionary<ModelGroup, Buffer<Vector3>> _vertexBufferLookup = new Dictionary<ModelGroup, Buffer<Vector3>>();
        private readonly Dictionary<ModelGroup, Buffer<Vector3>> _normalBufferLookup = new Dictionary<ModelGroup, Buffer<Vector3>>();
        private readonly Dictionary<ModelGroup, Buffer<Vector2>> _textureCoordinateBufferLookup = new Dictionary<ModelGroup, Buffer<Vector2>>();
        private readonly Dictionary<ModelGroup, Buffer<ushort>> _vertexIndexBufferLookup = new Dictionary<ModelGroup, Buffer<ushort>>();

        // Bounding boxes
        private readonly Dictionary<ModelGroup, RenderableBoundingBox> _boundingBoxLookup = new Dictionary<ModelGroup, RenderableBoundingBox>();

        // Doodad sets
        private readonly Dictionary<string, RenderableGameModel> _doodadCache = new Dictionary<string, RenderableGameModel>();
        private readonly Dictionary<string, List<ActorInstanceSet<RenderableGameModel>>> _doodadSets = new Dictionary<string, List<ActorInstanceSet<RenderableGameModel>>>();

        /// <inheritdoc />
        public int PolygonCount => this._model.Groups.Sum(g => g.GroupData.VertexIndices.VertexIndices.Count / 3);

        /// <inheritdoc />
        public int VertexCount => this._model.Groups.Sum(g => g.GroupData.Vertices.Vertices.Count);

        /// <inheritdoc />
        public bool IsInitialized { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not the bounding boxes of the model groups should be rendered.
        /// </summary>
        public bool ShouldRenderBounds { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not the wireframe of the object should be rendered.
        /// </summary>
        public bool ShouldRenderWireframe { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the doodads in the current doodad set should be rendered.
        /// </summary>
        public bool ShouldRenderDoodads { get; set; }

        /// <summary>
        /// Gets or sets the current doodad set.
        /// </summary>
        public string DoodadSet { get; set; }

        private WorldModelShader _shader;

        /// <summary>
        /// Initializes a new instance of the <see cref="RenderableWorldModel"/> class.
        /// </summary>
        /// <param name="inModel">The model to render.</param>
        /// <param name="gameContext">The game context.</param>
        public RenderableWorldModel(WMO inModel, WarcraftGameContext gameContext)
        {
            this._model = inModel;
            this._gameContext = gameContext;

            this.ActorTransform = new Transform();

            this.IsInitialized = false;
        }

        /// <inheritdoc />
        public void Initialize()
        {
            ThrowIfDisposed();

            if (this.IsInitialized)
            {
                return;
            }

            this.IsInitialized = true;

            this._shader = this._cache.GetShader(EverlookShader.WorldModel) as WorldModelShader;

            if (this._shader == null)
            {
                throw new ShaderNullException(typeof(WorldModelShader));
            }

            InitializeDoodads();

            // TODO: Load and cache sound emitters

            // Load the textures used in this model
            foreach (string texture in this._model.GetTextures())
            {
                if (!string.IsNullOrEmpty(texture))
                {
                    if (!this._textureLookup.ContainsKey(texture))
                    {
                        this._textureLookup.Add(texture, this._cache.GetTexture(texture, this._gameContext.Assets));
                    }
                }
            }

            // TODO: Upload visible block vertices

            // TODO: Upload portal vertices for debug rendering

            // TODO: Load lights into some sort of reasonable structure

            // TODO: Load fog as OpenGL fog

            // TODO: Implement antiportal handling. For now, skip them

            // TODO: Upload convex planes for debug rendering
            foreach (ModelGroup modelGroup in this._model.Groups)
            {
                InitializeModelGroup(modelGroup);
            }

            this.IsInitialized = true;
        }

        /// <summary>
        /// Initialize the OpenGL state of the world model's referenced doodads.
        /// </summary>
        private void InitializeDoodads()
        {
            foreach (var doodad in this._doodadCache.Select(d => d.Value))
            {
                doodad.Initialize();
            }

            foreach (var doodadSet in this._doodadSets)
            {
                foreach (var instanceSet in doodadSet.Value)
                {
                    instanceSet.Initialize();
                }
            }
        }

        /// <summary>
        /// Load all of the world model's referenced doodads into memory.
        /// </summary>
        public void LoadDoodads()
        {
            foreach (var doodadSet in this._model.RootInformation.DoodadSets.DoodadSets)
            {
                var doodadInstances = this._model.RootInformation.DoodadInstances.DoodadInstances
                    .Skip((int)doodadSet.FirstDoodadInstanceIndex)
                    .Take((int)doodadSet.DoodadInstanceCount).ToList();

                var doodadInstanceGroups = doodadInstances.GroupBy(d => d.Name);

                var doodadSetInstanceGroups = new List<ActorInstanceSet<RenderableGameModel>>();
                foreach (var doodadInstanceGroup in doodadInstanceGroups)
                {
                    var firstInstance = doodadInstanceGroup.First();

                    // Check and cache the doodad
                    if (string.IsNullOrEmpty(firstInstance.Name))
                    {
                        Log.Warn("Failed to load doodad. The instance name was null or empty.");
                        continue;
                    }

                    if (!this._doodadCache.ContainsKey(firstInstance.Name))
                    {
                        var doodadReference = this._gameContext.GetReferenceForDoodad(firstInstance);
                        var doodadModel = DataLoadingRoutines.LoadGameModel(doodadReference);

                        if (doodadModel == null)
                        {
                            Log.Warn($"Failed to load doodad \"{firstInstance.Name}\"");
                            continue;
                        }

                        // Then create a new renderable game model
                        var renderableDoodad = new RenderableGameModel(doodadModel, this._gameContext, firstInstance.Name);

                        // And cache it
                        this._doodadCache.Add(firstInstance.Name, renderableDoodad);
                    }

                    var instanceTransforms = new List<Transform>();
                    foreach (var doodadInstance in doodadInstanceGroup)
                    {
                        instanceTransforms.Add
                        (
                            new Transform
                            (
                                doodadInstance.Position.ToOpenGLVector(),
                                doodadInstance.Orientation.ToOpenGLQuaternion(),
                                new Vector3(doodadInstance.Scale)
                            )
                        );
                    }

                    var instanceSet = new ActorInstanceSet<RenderableGameModel>(this._doodadCache[firstInstance.Name]);
                    instanceSet.SetInstances(instanceTransforms);

                    doodadSetInstanceGroups.Add(instanceSet);
                }

                this._doodadSets.Add(doodadSet.Name, doodadSetInstanceGroups);
            }
        }

        /// <summary>
        /// Initialize the OpenGL state of the given model group.
        /// </summary>
        /// <param name="modelGroup">The model group to initialize.</param>
        private void InitializeModelGroup(ModelGroup modelGroup)
        {
            /*
                Buffers
            */

            Buffer<Vector3> vertexBuffer = new Buffer<Vector3>(BufferTarget.ArrayBuffer, BufferUsageHint.StaticDraw);
            Buffer<Vector3> normalBuffer = new Buffer<Vector3>(BufferTarget.ArrayBuffer, BufferUsageHint.StaticDraw);
            Buffer<Vector2> coordinateBuffer = new Buffer<Vector2>(BufferTarget.ArrayBuffer, BufferUsageHint.StaticDraw);
            Buffer<ushort> vertexIndexes = new Buffer<ushort>(BufferTarget.ElementArrayBuffer, BufferUsageHint.StaticDraw);

            // Upload all of the vertices in this group
            vertexBuffer.Data = modelGroup.GetVertices().Select(v => v.ToOpenGLVector()).ToArray();
            this._vertexBufferLookup.Add(modelGroup, vertexBuffer);

            vertexBuffer.AttachAttributePointer(new VertexAttributePointer(0, 3, VertexAttribPointerType.Float, 0, 0));

            // Upload all of the normals in this group
            normalBuffer.Data = modelGroup.GetNormals().Select(v => v.ToOpenGLVector()).ToArray();
            this._normalBufferLookup.Add(modelGroup, normalBuffer);

            normalBuffer.AttachAttributePointer(new VertexAttributePointer(1, 3, VertexAttribPointerType.Float, 0, 0));

            // Upload all of the UVs in this group
            coordinateBuffer.Data = modelGroup.GetTextureCoordinates().Select(v => v.ToOpenGLVector()).ToArray();
            this._textureCoordinateBufferLookup.Add(modelGroup, coordinateBuffer);

            coordinateBuffer.AttachAttributePointer(new VertexAttributePointer(2, 2, VertexAttribPointerType.Float, 0, 0));

            // Upload vertex indices for this group
            vertexIndexes.Data = modelGroup.GetVertexIndices().ToArray();
            this._vertexIndexBufferLookup.Add(modelGroup, vertexIndexes);

            RenderableBoundingBox boundingBox = new RenderableBoundingBox
            (
                modelGroup.GetBoundingBox(),
                this.ActorTransform
            );

            boundingBox.Initialize();

            this._boundingBoxLookup.Add(modelGroup, boundingBox);
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (!this.ShouldRenderDoodads)
            {
                return;
            }

            foreach (var doodad in this._doodadCache.Select(k => k.Value))
            {
                doodad.Tick(deltaTime);
            }
        }

        /// <inheritdoc />
        public void Render(Matrix4 viewMatrix, Matrix4 projectionMatrix, ViewportCamera camera)
        {
            ThrowIfDisposed();

            if (!this.IsInitialized)
            {
                return;
            }

            this._shader.Enable();
            this._shader.Wireframe.Enabled = this.ShouldRenderWireframe;
            if (this._shader.Wireframe.Enabled)
            {
                this._shader.Wireframe.SetWireframeLineWidth(2);
                this._shader.Wireframe.SetWireframeFadeWidth(2);
                this._shader.Wireframe.SetViewportMatrix(camera.GetViewportMatrix());
            }

            Matrix4 modelView = this.ActorTransform.GetModelMatrix() * viewMatrix;
            Matrix4 modelViewProjection = modelView * projectionMatrix;

            // TODO: Fix frustum culling
            foreach (ModelGroup modelGroup in this._model.Groups)
            {
                RenderGroup(modelGroup, modelViewProjection);
            }

            // Render bounding boxes
            if (this.ShouldRenderBounds)
            {
                // TODO: Ordering
                foreach (ModelGroup modelGroup in this._model.Groups)
                {
                    this._boundingBoxLookup[modelGroup].Render(viewMatrix, projectionMatrix, camera);
                }
            }

            if (this.ShouldRenderDoodads)
            {
                foreach (var doodadInstanceSet in this._doodadSets[this.DoodadSet])
                {
                    //doodadInstanceSet.ShouldRenderBounds = this.ShouldRenderBounds;
                }

                foreach (var doodadInstanceSet in this._doodadSets[this.DoodadSet])
                {
                    doodadInstanceSet.Render(viewMatrix, projectionMatrix, camera);
                }
            }

            // TODO: Summarize the render batches from each group that has the same material ID

            // TODO: Render each block of batches with the same material ID

            // TODO: Shade light effects and vertex colours

            // TODO: Render each doodad in the currently selected doodad set

            // TODO: Play sound emitters here?
        }

        /// <summary>
        /// Renders the specified model group on a batch basis.
        /// </summary>
        private void RenderGroup(ModelGroup modelGroup, Matrix4 modelViewProjection)
        {
            // Reenable depth test
            GL.Enable(EnableCap.DepthTest);

            // Render the object
            // Send the vertices to the shader
            this._vertexBufferLookup[modelGroup].Bind();
            this._vertexBufferLookup[modelGroup].EnableAttributes();

            this._normalBufferLookup[modelGroup].Bind();
            this._normalBufferLookup[modelGroup].EnableAttributes();

            this._textureCoordinateBufferLookup[modelGroup].Bind();
            this._textureCoordinateBufferLookup[modelGroup].EnableAttributes();

            // Bind the index buffer
            this._vertexIndexBufferLookup[modelGroup].Bind();

            if (this.ShouldRenderWireframe)
            {
                this._shader.Wireframe.SetWireframeColour(EverlookConfiguration.Instance.WireframeColour);

                // Override blend setting
                GL.Enable(EnableCap.Blend);
            }

            // Render all the different materials (opaque first, transparent after)
            foreach (RenderBatch renderBatch in modelGroup.GetRenderBatches()
                .OrderBy(batch => batch.MaterialIndex)
                .ThenBy(batch => this._model.GetMaterial(batch.MaterialIndex).BlendMode))
            {
                this._shader.Enable();

                ModelMaterial modelMaterial = this._model.GetMaterial(renderBatch.MaterialIndex);

                this._shader.SetMaterial(modelMaterial);
                this._shader.SetMVPMatrix(modelViewProjection);

                // Set the texture as the first diffuse texture in unit 0
                Texture2D texture = this._cache.GetCachedTexture(modelMaterial.DiffuseTexture);
                if (modelMaterial.Flags.HasFlag(MaterialFlags.TextureWrappingClampS))
                {
                    texture.WrappingMode = TextureWrapMode.Clamp;
                }
                else
                {
                    texture.WrappingMode = TextureWrapMode.Repeat;
                }

                this._shader.BindTexture2D(TextureUnit.Texture0, TextureUniform.Texture0, texture);

                // Finally, draw the model
                GL.DrawRangeElements
                (
                    PrimitiveType.Triangles,
                    renderBatch.FirstPolygonIndex,
                    renderBatch.FirstPolygonIndex + renderBatch.PolygonIndexCount - 1,
                    renderBatch.PolygonIndexCount,
                    DrawElementsType.UnsignedShort,
                    new IntPtr(renderBatch.FirstPolygonIndex * 2)
                );
            }

            // Release the attribute arrays
            this._vertexBufferLookup[modelGroup].DisableAttributes();
            this._normalBufferLookup[modelGroup].DisableAttributes();
            this._textureCoordinateBufferLookup[modelGroup].DisableAttributes();
        }

        /// <summary>
        /// Gets the names of the doodad sets for this model.
        /// </summary>
        /// <returns>The names of the doodad sets.</returns>
        public IEnumerable<string> GetDoodadSetNames()
        {
            return this._model.RootInformation.DoodadSets.DoodadSets.Select(ds => ds.Name);
        }

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/> if the object has been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the object is disposed.</exception>
        private void ThrowIfDisposed()
        {
            if (this.IsDisposed)
            {
                throw new ObjectDisposedException(ToString());
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.IsDisposed = true;

            foreach (var vertexBuffer in this._vertexBufferLookup)
            {
                vertexBuffer.Value.Dispose();
            }

            foreach (var normalBuffer in this._normalBufferLookup)
            {
                normalBuffer.Value.Dispose();
            }

            foreach (var coordinateBuffer in this._textureCoordinateBufferLookup)
            {
                coordinateBuffer.Value.Dispose();
            }

            foreach (var indexBuffer in this._vertexIndexBufferLookup)
            {
                indexBuffer.Value.Dispose();
            }

            foreach (var doodad in this._doodadCache.Select(k => k.Value))
            {
                doodad.Dispose();
            }
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            var otherModel = obj as RenderableWorldModel;
            if (otherModel == null)
            {
                return false;
            }

            return (otherModel._model == this._model) &&
                    (otherModel._gameContext == this._gameContext) &&
                    (otherModel.IsStatic == this.IsStatic);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return (this.IsStatic.GetHashCode() + this._model.GetHashCode() + this._gameContext.GetHashCode()).GetHashCode();
        }
    }
}
