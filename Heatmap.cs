//-----------------------------------------------------------------------
// <copyright file="Heatmap.cs" company="Studio A&T s.r.l.">
//     Heatmap generation code from https://github.com/jondot/heatmapdotnet
//     Did some modifs/tests in it :-)
// </copyright>
// <author>nicogis</author>
//-----------------------------------------------------------------------
namespace Studioat.ArcGis.Soi.ImageProcessing
{
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    
    /// <summary>
    /// class Heatmap
    /// </summary>
    public class Heatmap
    {
        /// <summary>
        /// generate heatmap
        /// </summary>
        /// <param name="width">dimension width</param>
        /// <param name="height">dimension height</param>
        /// <param name="x">array point x</param>
        /// <param name="y">array point y</param>
        /// <returns>image heatmap</returns>
        public static Image GenerateHeatMap(int width, int height, float[] x, float[] y)
        {
            // Create canvas the size of the page
            Image canvastest = null;

            try
            {
                canvastest = new Bitmap(width, height);

                // Load the dot-Image
                using (Image pt = (Image)Resource.ResourceManager.GetObject("heatdot"), canvas = new Bitmap(width, height))
                {
                    // Initialize Graphics object to work on the canvas
                    using (Graphics g = Graphics.FromImage(canvas))
                    {
                        g.Clear(Color.White);

                        for (int i = 0; i < x.Length; i++)
                        {
                            g.DrawImage(pt, x[i] - (pt.Width / 2), y[i] - (pt.Height / 2));
                        }

                        // Create a new ImageAttributes object to manipulate the Image
                        ImageAttributes imageAttributes = new ImageAttributes();
                        ColorMap[] remapTable = new ColorMap[255];

                        // Replace OldColor with a NewColor for all color-codes from 0,0,0 to 75, 75, 75 (RGB) 
                        // (From black to dark-gray)
                        for (int i = 0; i < 75; i++)
                        {
                            ColorMap c = new ColorMap();
                            c.OldColor = Color.FromArgb(i, i, i);
                            c.NewColor = Color.FromArgb(255 - i, 0, 0);
                            remapTable[i] = c;
                        }

                        // Replace OldColor with a NewColor for all color-codes from 75, 75, 75 to 200, 200, 200 (RGB) 
                        // (From dark-gray to gray)
                        for (int i = 75; i < 200; i++)
                        {
                            ColorMap c = new ColorMap();
                            c.OldColor = Color.FromArgb(i, i, i);
                            c.NewColor = Color.FromArgb(0, 255 - i, 0);
                            remapTable[i] = c;
                        }

                        // Replace OldColor with a NewColor for all color-codes from 200, 200, 200 to 255, 255, 255 (RGB) 
                        // (From gray to light-gray - before it gets white!)
                        for (int i = 200; i < 255; i++)
                        {
                            ColorMap c = new ColorMap();
                            c.OldColor = Color.FromArgb(i, i, i);
                            c.NewColor = Color.FromArgb(0, 0, i - 100);
                            remapTable[i] = c;
                        }

                        // Set the RemapTable on the ImageAttributes object.
                        imageAttributes.SetRemapTable(remapTable, ColorAdjustType.Bitmap);

                        // Draw Image with the new ImageAttributes
                        g.DrawImage(canvas, new Rectangle(0, 0, canvas.Width, canvas.Height), 0, 0, canvas.Width, canvas.Height, GraphicsUnit.Pixel, imageAttributes);

                        // Replace the white color with the same color as the edge of all the dots. 
                        ImageAttributes ia = new ImageAttributes();
                        ColorMap[] cm = new ColorMap[1];
                        ColorMap cw = new ColorMap();
                        cw.OldColor = Color.White;
                        cw.NewColor = Color.FromArgb(0, 0, 0);
                        cm[0] = cw;

                        // Set the RemapTable on the new ImageAttributes object.
                        ia.SetRemapTable(cm, ColorAdjustType.Bitmap);

                        // Draw the Image again, with the new ImageAttributes.
                        g.DrawImage(canvas, new Rectangle(0, 0, canvas.Width, canvas.Height), 0, 0, canvas.Width, canvas.Height, GraphicsUnit.Pixel, ia);

                        // Setting transparency!
                        // Create a new color matrix and set the alpha value to 0.5
                        ColorMatrix cam = new ColorMatrix();
                        cam.Matrix00 = cam.Matrix11 = cam.Matrix22 = cam.Matrix44 = 1;
                        cam.Matrix33 = Convert.ToSingle(0.5);

                        // Create a new image attribute object and set the color matrix to the one just created
                        ImageAttributes iaa = new ImageAttributes();
                        iaa.SetColorMatrix(cam);

                        // Draw the original image with the image attributes specified
                        g.DrawImage(canvas, new Rectangle(0, 0, canvas.Width, canvas.Height), 0, 0, canvas.Width, canvas.Height, GraphicsUnit.Pixel, iaa);

                        // overlay heatmap
                        using (Graphics g2 = Graphics.FromImage(canvastest))
                        {
                            g2.DrawImage(canvas, new Rectangle(0, 0, canvas.Width, canvas.Height), 0, 0, canvas.Width, canvas.Height, GraphicsUnit.Pixel, iaa);
                        }
                    }
                }    
            }
            catch
            {
                if (canvastest != null)
                {
                    canvastest.Dispose();
                }
            }

            return canvastest;
        }
    }
}
