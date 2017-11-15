//-----------------------------------------------------------------------
// <copyright file="Helper.cs" company="Studio A&T s.r.l.">
//     Copyright (c) Studio A&T s.r.l. All rights reserved.
// </copyright>
// <author>Nicogis</author>
//-----------------------------------------------------------------------
namespace Studioat.ArcGis.Soi.ImageProcessing
{
    internal enum ImageProcessingOp
    {
        NoImageProcessing,
        Heatmap,
        GaussianSharpen,
        GaussianBlur,
        Tint,
        Pixelate
    }
}
