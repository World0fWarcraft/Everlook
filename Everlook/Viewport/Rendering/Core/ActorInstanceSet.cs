﻿//
//  ActorInstanceSet.cs
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
using System.Runtime.InteropServices;
using Everlook.Viewport.Camera;
using Everlook.Viewport.Rendering.Interfaces;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace Everlook.Viewport.Rendering.Core
{
    /// <summary>
    /// Represets a set of actor instances, differing by their transforms.
    /// </summary>
    /// <typeparam name="T">A renderable supporting instanced rendering.</typeparam>
    public class ActorInstanceSet<T> : IRenderable, IBoundedModel
        where T : class, IInstancedRenderable, IActor, IBoundedModel
    {
        /// <inheritdoc />
        public bool ShouldRenderBounds
        {
            get => this.Target.ShouldRenderBounds;
            set => this.Target.ShouldRenderBounds = value;
        }

        /// <inheritdoc />
        public bool IsStatic => this.Target.IsStatic;

        /// <inheritdoc />
        public bool IsInitialized { get; set; }

        /// <inheritdoc />
        public ProjectionType Projection => this.Target.Projection;

        /// <summary>
        /// Gets the target renderable of the instance set.
        /// </summary>
        public T Target { get; }

        /// <summary>
        /// Gets the number of instances in the set.
        /// </summary>
        public int Count => _instanceTransforms.Count;

        private Buffer<Matrix4>? _instanceModelMatrices;

        private List<Transform> _instanceTransforms;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorInstanceSet{T}"/> class.
        /// </summary>
        /// <param name="target">The target instanced renderable.</param>
        public ActorInstanceSet(T target)
        {
            this.Target = target;
            _instanceTransforms = new List<Transform>();

            this.IsInitialized = false;
        }

        /// <summary>
        /// Sets the transforms of the instances in the set.
        /// </summary>
        /// <param name="instanceTransforms">The transforms of the instances.</param>
        public void SetInstances(IEnumerable<Transform> instanceTransforms)
        {
            _instanceTransforms = instanceTransforms.ToList();
        }

        /// <inheritdoc />
        public void Initialize()
        {
            _instanceModelMatrices = new Buffer<Matrix4>(BufferTarget.ArrayBuffer, BufferUsageHint.StaticDraw);

            _instanceModelMatrices.AttachAttributePointer
            (
                new VertexAttributePointer(6, 4, VertexAttribPointerType.Float, Marshal.SizeOf<Matrix4>(), 0)
            );

            _instanceModelMatrices.AttachAttributePointer
            (
                new VertexAttributePointer(7, 4, VertexAttribPointerType.Float, Marshal.SizeOf<Matrix4>(), 16)
            );

            _instanceModelMatrices.AttachAttributePointer
            (
                new VertexAttributePointer(8, 4, VertexAttribPointerType.Float, Marshal.SizeOf<Matrix4>(), 32)
            );

            _instanceModelMatrices.AttachAttributePointer
            (
                new VertexAttributePointer(9, 4, VertexAttribPointerType.Float, Marshal.SizeOf<Matrix4>(), 48)
            );

            _instanceModelMatrices.Bind();
            _instanceModelMatrices.EnableAttributes();
            GL.VertexAttribDivisor(6, 1);
            GL.VertexAttribDivisor(7, 1);
            GL.VertexAttribDivisor(8, 1);
            GL.VertexAttribDivisor(9, 1);

            _instanceModelMatrices.Data = _instanceTransforms.Select(t => t.GetModelMatrix()).ToArray();

            this.IsInitialized = true;
        }

        /// <inheritdoc />
        public void Render(Matrix4 viewMatrix, Matrix4 projectionMatrix, ViewportCamera camera)
        {
            if (!this.IsInitialized || _instanceModelMatrices is null)
            {
                return;
            }

            _instanceModelMatrices.Bind();
            _instanceModelMatrices.EnableAttributes();

            this.Target.RenderInstances(viewMatrix, projectionMatrix, camera, this.Count);

            _instanceModelMatrices.DisableAttributes();
        }

        /// <summary>
        /// This method does nothing, and should not be called. The source actor should be disposed instead.
        /// </summary>
        public void Dispose()
        {
            throw new NotSupportedException
            (
                "A renderable instance set should not be disposed. Dispose the source actor instead."
            );
        }
    }
}
