﻿//
//  EverlookDirectoryExportDialogElements.cs
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

using Gtk;
using UIElement = Gtk.Builder.ObjectAttribute;

// ReSharper disable UnassignedReadonlyField
#pragma warning disable 649
#pragma warning disable 1591
#pragma warning disable SA1134 // Each attribute should be placed on its own line of code

namespace Everlook.UI
{
    public sealed partial class EverlookDirectoryExportDialog
    {
        [UIElement] private readonly ListStore _itemExportListStore = null!;
        [UIElement] private readonly TreeView _itemListingTreeView = null!;
        [UIElement] private readonly CellRendererToggle _exportItemToggleRenderer = null!;

        [UIElement] private readonly Menu _exportPopupMenu = null!;
        [UIElement] private readonly ImageMenuItem _selectAllItem = null!;
        [UIElement] private readonly ImageMenuItem _selectNoneItem = null!;

        [UIElement] private readonly FileChooserButton _exportDirectoryFileChooserButton = null!;
    }
}
