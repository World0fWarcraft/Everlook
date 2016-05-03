﻿//
//  VirtualItemReference.cs
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) 2016 Jarl Gullberg
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
using Everlook.Package;

namespace Everlook
{
	/// <summary>
	/// A virtual item reference. This type of item reference does not point to a specific file in a package, but
	/// rather acts as a shimmy on top of a number of underlying item references. It has a main "hard" item reference
	/// which is its primary target, and a number of other item references (0 or more) that are overridden by this
	/// hard reference.
	/// </summary>
	public class VirtualItemReference : ItemReference
	{
		/// <summary>
		/// Gets the hard reference. The hard reference is the primary underlying reference to which 
		/// this virtual reference points.
		/// </summary>
		/// <value>The hard reference.</value>
		public ItemReference HardReference
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the overridden hard references. This is a list of hard references that are overridden by
		/// the primary hard reference. See: (<see cref="HardReference"/>)
		/// </summary>
		/// <value>The overridden hard references.</value>
		public List<ItemReference> OverriddenHardReferences
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets or sets the name of the package where the file is stored.
		/// </summary>
		/// <value>The name of the package.</value>
		public override string PackageName
		{
			get
			{
				return "";
			}
			set
			{
				throw new InvalidOperationException("The package name may not be set on a virtual reference.");
			}
		}

		/// <summary>
		/// Gets or sets the file path of the file inside the package.
		/// </summary>
		/// <value>The file path.</value>
		public override string ItemPath
		{
			get
			{
				return this.HardReference.ItemPath;
			}
			set
			{
				if (value != this.HardReference.ItemPath)
				{
					throw new InvalidOperationException("The item path may not point to a file other than the one the base reference points to.");
				}

				base.ItemPath = value;
			}
		}

		/// <summary>
		/// Gets a value indicating whether this or not this reference is a package reference.
		/// </summary>
		/// <value>true</value>
		/// <c>false</c>
		public override bool IsPackage
		{
			get
			{
				return false;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.VirtualItemReference"/> class.
		/// </summary>
		/// <param name="InHardReference">The primary hard reference this virtual reference points to.</param>
		/// <param name="InGroup">The package group this reference is a part of.</param>
		public VirtualItemReference(PackageGroup InGroup, ItemReference InHardReference)
		{			
			this.OverriddenHardReferences = new List<ItemReference>();

			this.Group = InGroup;
			this.HardReference = InHardReference;
			this.ItemPath = this.HardReference.ItemPath;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Everlook.VirtualItemReference"/> class, 
		/// where the reference has a parent <see cref="Everlook.VirtualItemReference"/>.
		/// </summary>
		/// <param name="ParentVirtualReference">Parent virtual reference.</param>
		/// <param name="InGroup">In group.</param>
		/// <param name="InHardReference">In hard reference.</param>
		public VirtualItemReference(VirtualItemReference ParentVirtualReference, PackageGroup InGroup, ItemReference InHardReference)
			: this(InGroup, InHardReference)
		{
			this.ParentReference = ParentVirtualReference;	
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="Everlook.VirtualItemReference"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode()
		{
			return (base.GetHashCode() + this.HardReference.GetHashCode()).GetHashCode();
		}
	}
}

