﻿// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
//  Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Stride.BepuPhysics.Constraints;

public interface ITwoBody
{
    public BodyComponent? A { get; set; }
    public BodyComponent? B { get; set; }
}
