//-----------------------------------------------------------------------
// <copyright file="ImageProcessing.cs" company="Studio A&T s.r.l.">
//     Copyright (c) Studio A&T s.r.l. All rights reserved.
// </copyright>
// <author>Nicogis</author>
//-----------------------------------------------------------------------
namespace Studioat.ArcGis.Soi.ImageProcessing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using ESRI.ArcGIS.Carto;
    using ESRI.ArcGIS.esriSystem;
    using ESRI.ArcGIS.Geodatabase;
    using ESRI.ArcGIS.Geometry;
    using ESRI.ArcGIS.Server;
    using ESRI.ArcGIS.SOESupport;
    using ImageProcessor;

    /// <summary>
    /// class Image Processing SOI
    /// </summary>
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation", Justification = "Reviewed.")]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:FieldNamesMustBeginWithLowerCaseLetter", Justification = "Reviewed.")]
    [ComVisible(true)]
    [Guid("F30D9FFA-4D3B-4CCF-B823-FDFA523D161F")]
    [ClassInterface(ClassInterfaceType.None)]
    [ServerObjectInterceptor("MapServer",
        Description = "Image processing SOI",
        DisplayName = "Image processing SOI",
        Properties = "IdLayerHeatMap=;ImageProcessing=NoImageProcessing;GaussianSigma=1.4;GaussianSize=10;GaussianThreshold=0;TintColor=255,0,0;PixelatePixelSize=1", HasManagerPropertiesConfigurationPane = false)]
    public class ImageProcessing : IServerObjectExtension, IObjectConstruct, IRESTRequestHandler, IWebRequestHandler, IRequestHandler2, IRequestHandler
    {
        /// <summary>
        /// soi name
        /// </summary>
        private string soiName;

        /// <summary>
        /// server object Helper
        /// </summary>
        private IServerObjectHelper serverObjectHelper;

        /// <summary>
        /// server log
        /// </summary>
        private ServerLogger serverLog;

        /// <summary>
        /// extension cache
        /// </summary>
        private Dictionary<string, IServerObjectExtension> extensionCache = new Dictionary<string, IServerObjectExtension>();

        /// <summary>
        /// server Environment
        /// </summary>
        private IServerEnvironment2 serverEnvironment;

        /// <summary>
        /// feature class point for heatmap
        /// </summary>
        private IFeatureClass featureClassPointHeatMap = null;

        /// <summary>
        /// id layer for heatmap
        /// </summary>
        private int? idLayerHeatMap = null;

        /// <summary>
        /// message code
        /// </summary>
        private int msgCode = 250;

        /// <summary>
        /// gaussian layer (gaussian blur and sharpen)
        /// </summary>
        private ImageProcessor.Imaging.GaussianLayer gaussianLayer = null;

        /// <summary>
        /// color (tint)
        /// </summary>
        private Color tintColor = Color.FromArgb(255, 0, 0);

        /// <summary>
        /// method used soi
        /// </summary>
        private ImageProcessingOp imageProcessingOp = ImageProcessingOp.NoImageProcessing;

        /// <summary>
        /// pixel size (pixelate)
        /// </summary>
        private int? pixelatePixelSize = 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageProcessing"/> class.
        /// </summary>
        public ImageProcessing()
        {
            this.soiName = this.GetType().Name;
        }

        /// <summary>
        /// Gets Server Environment
        /// </summary>
        private IServerEnvironment2 ServerEnvironment
        {
            get
            {
                if (this.serverEnvironment == null)
                {
                    UID uid = new UIDClass();
                    uid.Value = "{32D4C328-E473-4615-922C-63C108F55E60}";

                    // CoCreate an EnvironmentManager and retrieve the IServerEnvironment
                    IEnvironmentManager environmentManager = new EnvironmentManager() as IEnvironmentManager;
                    this.serverEnvironment = environmentManager.GetEnvironment(uid) as IServerEnvironment2;
                }

                return this.serverEnvironment;
            }
        }

        /// <summary>
        /// init of soi
        /// </summary>
        /// <param name="pSOH">Server Object Helper</param>
        public void Init(IServerObjectHelper pSOH)
        {
            this.serverObjectHelper = pSOH;
            this.serverLog = new ServerLogger();

            //// System.Diagnostics.Debugger.Launch();

            this.serverLog.LogMessage(ServerLogger.msgType.infoStandard,  $"{this.soiName}.{nameof(this.Init)}()", this.msgCode, $"Initialized {soiName} SOI.");
        }

        /// <summary>
        /// construct of soi
        /// </summary>
        /// <param name="props">properties of soi</param>
        public void Construct(IPropertySet props)
        {
            try
            {
                string s = props.GetProperty("ImageProcessing") as string;
                if (string.IsNullOrWhiteSpace(s))
                {
                    throw new Exception();
                }

                this.imageProcessingOp = (ImageProcessingOp)Enum.Parse(typeof(ImageProcessingOp), s);
            }
            catch
            {
                this.serverLog.LogMessage(ServerLogger.msgType.warning, $"{this.soiName}.{nameof(this.Construct)}()", this.msgCode, $"No value or invalid value provided for {nameof(this.imageProcessingOp)}.");
                this.imageProcessingOp = ImageProcessingOp.NoImageProcessing;
            }

            if (this.imageProcessingOp == ImageProcessingOp.Heatmap)
            {
                try
                {
                    this.idLayerHeatMap = Convert.ToInt32(props.GetProperty("IdLayerHeatMap"));
                }
                catch
                {
                    this.serverLog.LogMessage(ServerLogger.msgType.warning, $"{this.soiName}.{nameof(this.Construct)}()", this.msgCode, $"No value or invalid value provided for {nameof(this.idLayerHeatMap)}.");
                    this.idLayerHeatMap = null;
                }

                if (this.idLayerHeatMap.HasValue)
                {
                    try
                    {
                        IMapServer3 mapServer = (IMapServer3)this.serverObjectHelper.ServerObject;

                        // Access the source feature class of the layer data source (to request it on map export :-))
                        IMapServerDataAccess dataAccess = (IMapServerDataAccess)mapServer;
                        this.featureClassPointHeatMap = (IFeatureClass)dataAccess.GetDataSource(mapServer.DefaultMapName, this.idLayerHeatMap.Value);

                        if ((this.featureClassPointHeatMap == null) || (this.featureClassPointHeatMap.ShapeType != esriGeometryType.esriGeometryPoint))
                        {
                            this.idLayerHeatMap = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        this.serverLog.LogMessage(ServerLogger.msgType.warning, $"{this.soiName}.{nameof(this.Construct)}()", this.msgCode, $"No value or invalid value provided for {nameof(this.idLayerHeatMap)}. Error: {ex.Message}.");
                        this.idLayerHeatMap = null;
                    }                   
                }
            }
            else if ((this.imageProcessingOp == ImageProcessingOp.GaussianBlur) || (this.imageProcessingOp == ImageProcessingOp.GaussianSharpen))
            {
                this.gaussianLayer = new ImageProcessor.Imaging.GaussianLayer(10);
                
                try
                {
                    this.gaussianLayer.Sigma = Convert.ToDouble(props.GetProperty("GaussianSigma"), CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    this.serverLog.LogMessage(ServerLogger.msgType.warning, $"{this.soiName}.{nameof(this.Construct)}()", this.msgCode, $"No value or invalid value provided for {nameof(this.gaussianLayer.Sigma)}. Error: {ex.Message}");
                    this.gaussianLayer.Sigma = 1.4;
                }

                try
                {
                    this.gaussianLayer.Size = Convert.ToInt32(props.GetProperty("GaussianSize"));
                }
                catch (Exception ex)
                {
                    this.serverLog.LogMessage(ServerLogger.msgType.warning, $"{this.soiName}.{nameof(this.Construct)}()", this.msgCode, $"No value or invalid value provided for {nameof(this.gaussianLayer.Size)}. Error: {ex.Message}");
                    this.gaussianLayer.Size = 10;
                }

                try
                {
                    this.gaussianLayer.Threshold = Convert.ToInt32(props.GetProperty("GaussianThreshold"));
                }
                catch (Exception ex)
                {
                    this.serverLog.LogMessage(ServerLogger.msgType.warning, $"{this.soiName}.{nameof(this.Construct)}()", this.msgCode, $"No value or invalid value provided for {nameof(this.gaussianLayer.Threshold)}. Error: {ex.Message}");
                    this.gaussianLayer.Threshold = 0;
                }
            }
            else if (this.imageProcessingOp == ImageProcessingOp.Tint)
            {
                try
                {
                    string tintColor = Convert.ToString(props.GetProperty("TintColor"));
                    Color r = Color.FromName(tintColor);
                    if (r.IsKnownColor)
                    {
                        this.tintColor = r;
                    }
                    else
                    {
                        var s = tintColor.Split(',').Select(k => int.Parse(k)).ToArray();
                        this.tintColor = Color.FromArgb(s[0], s[1], s[2]);
                    }
                }
                catch (Exception ex)
                {
                    this.serverLog.LogMessage(ServerLogger.msgType.warning, $"{this.soiName}.{nameof(this.Construct)}()", this.msgCode, $"No value or invalid value provided for {nameof(this.tintColor)}. Error: {ex.Message}");
                    this.tintColor = Color.FromArgb(255, 0, 0);
                }
            }
            else if (this.imageProcessingOp == ImageProcessingOp.Pixelate)
            {
                try
                {
                    this.pixelatePixelSize = Convert.ToInt32(props.GetProperty("PixelatePixelSize"));
                }
                catch (Exception ex)
                {
                    this.serverLog.LogMessage(ServerLogger.msgType.warning, $"{this.soiName}.{nameof(this.Construct)}()", this.msgCode, $"No value or invalid value provided for {nameof(this.pixelatePixelSize)}. Error: {ex.Message}");
                    this.pixelatePixelSize = 1;
                }
            }
        }

        /// <summary>
        /// shut down of soi
        /// </summary>
        public void Shutdown()
        {
            this.serverLog.LogMessage(ServerLogger.msgType.infoStandard, $"{this.soiName}.{nameof(this.Shutdown)}()", this.msgCode, $"Shutting down {soiName} SOI.");
        }

        #region REST interceptors

        /// <summary>
        /// get schema
        /// </summary>
        /// <returns>get schema of soi</returns>
        public string GetSchema()
        {
            IRESTRequestHandler restRequestHandler = FindRequestHandlerDelegate<IRESTRequestHandler>();
            if (restRequestHandler == null)
            {
                return null;
            }

            return restRequestHandler.GetSchema();
        }

        /// <summary>
        /// Handle REST Request
        /// </summary>
        /// <param name="Capabilities">list of capabilities</param>
        /// <param name="resourceName">resource Name</param>
        /// <param name="operationName">operation Name</param>
        /// <param name="operationInput">operation Input</param>
        /// <param name="outputFormat">output Format</param>
        /// <param name="requestProperties">request Properties</param>
        /// <param name="responseProperties">response Properties</param>
        /// <returns>response rest</returns>
        public byte[] HandleRESTRequest(string Capabilities, string resourceName, string operationName, string operationInput, string outputFormat, string requestProperties, out string responseProperties)
        {
            responseProperties = null;
            this.serverLog.LogMessage(ServerLogger.msgType.infoStandard, $"{this.soiName}.{nameof(this.HandleRESTRequest)}()", this.msgCode, $"Request received in Server Object Interceptor for {nameof(this.HandleRESTRequest)}");

            if (this.imageProcessingOp == ImageProcessingOp.Heatmap && this.idLayerHeatMap.HasValue)
            {
                // when we call export map operation will generate heatmap only on rest image export that request an image. (could also request json, and then we need to save image on disk and send json response)
                if (operationName.Equals("export", StringComparison.CurrentCultureIgnoreCase) && outputFormat.Equals("image", StringComparison.CurrentCultureIgnoreCase))
                {
                        var json = new JsonObject(operationInput);

                        // get export map parameters
                        string bbox;
                        string size;
                        json.TryGetString("bbox", out bbox);
                        json.TryGetString("size", out size);
                        string[] extentString = bbox.Split(',');
                        var width = int.Parse(size.Split(',')[0]);
                        var height = int.Parse(size.Split(',')[1]);

                        // save values
                        var xmin = double.Parse(extentString[0], CultureInfo.InvariantCulture);
                        var ymin = double.Parse(extentString[1], CultureInfo.InvariantCulture);
                        var xmax = double.Parse(extentString[2], CultureInfo.InvariantCulture);
                        var ymax = double.Parse(extentString[3], CultureInfo.InvariantCulture);

                        var xVal = xmax - xmin;
                        var yVal = ymax - ymin;

                        // create envelope
                        IEnvelope envelope = new EnvelopeClass();
                        envelope.XMin = xmin;
                        envelope.YMin = ymin;
                        envelope.XMax = xmax;
                        envelope.YMax = ymax;

                        // query points into enveloppe and iterate.
                        this.serverLog.LogMessage(ServerLogger.msgType.infoStandard, $"{this.soiName}.{nameof(this.HandleRESTRequest)}()", this.msgCode, "startquery");

                        ISpatialFilter filter = new SpatialFilterClass();
                        filter.Geometry = envelope;
                        filter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;
                        IFeatureCursor featureCursor = null;
                        List<float> xList = null;
                        List<float> yList = null;

                        using (ComReleaser comReleaser = new ComReleaser())
                        {
                            featureCursor = this.featureClassPointHeatMap.Search(filter, true);
                            comReleaser.ManageLifetime(featureCursor);

                            IFeature feature = null;
                            xList = new List<float>();
                            yList = new List<float>();

                            while ((feature = featureCursor.NextFeature()) != null)
                            {
                                // code to "project points" from real life coordinates inside the envelope, to relative image pixels coordinates.
                                IPoint point = feature.Shape as IPoint;
                                var baseX = point.X - xmin;
                                var baseY = point.Y - ymin;

                                var relativeX = baseX * width / xVal;
                                xList.Add((float)relativeX);

                                // damn, envelope start left bottom, and image left top ! :-(
                                var relativeY = height - (baseY * height / yVal);
                                yList.Add((float)relativeY);
                            }
                        }

                        this.serverLog.LogMessage(ServerLogger.msgType.infoStandard, $"{this.soiName}.${nameof(this.HandleRESTRequest)}()", this.msgCode, "endquery");

                        // then call an external lib, to generate an image.
                        // the image returned is 32 bpp, should find a way to get it to indexed 8 bpp for file size
                        byte[] b = null;

                        using (Image heatmap = Heatmap.GenerateHeatMap(width, height, xList.ToArray(), yList.ToArray()))
                        {
                            using (var newResponse = new System.IO.MemoryStream())
                            {
                                heatmap.Save(newResponse, ImageFormat.Png);
                                b = newResponse.GetBuffer();
                            }
                        }

                        //// then we send our own response without calling the standard "export map".
                        return b;
                    }         
            }

            // Find the correct delegate to forward the request too
            IRESTRequestHandler restRequestHandler = this.FindRequestHandlerDelegate<IRESTRequestHandler>();
            if (restRequestHandler == null)
            {
                return null;
            }

            var response = restRequestHandler.HandleRESTRequest(Capabilities, resourceName, operationName, operationInput, outputFormat, requestProperties, out responseProperties);

            if (this.imageProcessingOp != ImageProcessingOp.NoImageProcessing)
            {
                // when we call export map operation will generate heatmap only on rest image export that request an image. (could also request json, and then we need to save image on disk and send json response)
                if (operationName.Equals("export", StringComparison.CurrentCultureIgnoreCase) && outputFormat.Equals("image", StringComparison.CurrentCultureIgnoreCase))
                {
                    try
                    {
                        using (MemoryStream inStream = new MemoryStream(response))
                        {
                            using (MemoryStream outStream = new MemoryStream())
                            {
                                using (ImageFactory imageFactory = new ImageFactory(true))
                                {
                                    if (this.imageProcessingOp == ImageProcessingOp.GaussianBlur)
                                    {
                                        imageFactory.Load(inStream).GaussianBlur(this.gaussianLayer).Save(outStream);
                                    }
                                    else if (this.imageProcessingOp == ImageProcessingOp.GaussianSharpen)
                                    {
                                        imageFactory.Load(inStream).GaussianSharpen(this.gaussianLayer).Save(outStream);
                                    }
                                    else if (this.imageProcessingOp == ImageProcessingOp.Tint)
                                    {
                                        imageFactory.Load(inStream).Tint(this.tintColor).Save(outStream);
                                    }
                                    else if (this.imageProcessingOp == ImageProcessingOp.Pixelate)
                                    {
                                        imageFactory.Load(inStream).Pixelate(this.pixelatePixelSize.Value).Save(outStream);
                                    }
                                }

                                response = outStream.ToArray();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        this.serverLog.LogMessage(ServerLogger.msgType.error, $"{this.soiName}.{nameof(this.HandleRESTRequest)}()", this.msgCode, $"Request received in Server Object Interceptor for {nameof(this.HandleRESTRequest)}. Error {ex.Message}");
                    }
                }
            }

            return response;
        }

        #endregion

        #region SOAP interceptors

        /// <summary>
        /// request OGC
        /// </summary>
        /// <param name="httpMethod">method http</param>
        /// <param name="requestURL">request url</param>
        /// <param name="queryString">query string</param>
        /// <param name="Capabilities">list of capabilities</param>
        /// <param name="requestData">request Data</param>
        /// <param name="responseContentType">response Content Type</param>
        /// <param name="respDataType">response Data Type</param>
        /// <returns>Web Request</returns>
        public byte[] HandleStringWebRequest(esriHttpMethod httpMethod, string requestURL, string queryString, string Capabilities, string requestData, out string responseContentType, out esriWebResponseDataType respDataType)
        {
            this.serverLog.LogMessage(ServerLogger.msgType.infoStandard, $"{this.soiName}.{nameof(this.HandleStringWebRequest)}()", this.msgCode, $"Request received in Server Object Interceptor for {nameof(this.HandleStringWebRequest)}");

            /*
             * Add code to manipulate requests here
             */

            IWebRequestHandler webRequestHandler = this.FindRequestHandlerDelegate<IWebRequestHandler>();
            if (webRequestHandler != null)
            {
                return webRequestHandler.HandleStringWebRequest(
                        httpMethod, requestURL, queryString, Capabilities, requestData, out responseContentType, out respDataType);
            }

            responseContentType = null;
            respDataType = esriWebResponseDataType.esriWRDTPayload;

            // Insert error response here.
            return null;
        }

        /// <summary>
        /// request soap binary
        /// </summary>
        /// <param name="request">request soap</param>
        /// <returns>response soap binary</returns>
        public byte[] HandleBinaryRequest(ref byte[] request)
        {
            this.serverLog.LogMessage(ServerLogger.msgType.infoStandard, $"{this.soiName}.{nameof(this.HandleBinaryRequest)}()", this.msgCode, $"Request received in Sample Object Interceptor for {nameof(this.HandleBinaryRequest)}");

            /*
             * Add code to manipulate requests here
             */

            IRequestHandler requestHandler = FindRequestHandlerDelegate<IRequestHandler>();
            if (requestHandler != null)
            {
                return requestHandler.HandleBinaryRequest(request);
            }

            //// Insert error response here.
            return null;
        }

        /// <summary>
        /// request soap binary
        /// </summary>
        /// <param name="Capabilities">list of capabilities</param>
        /// <param name="request">request soap</param>
        /// <returns>response soap binary</returns>
        public byte[] HandleBinaryRequest2(string Capabilities, ref byte[] request)
        {
            this.serverLog.LogMessage(ServerLogger.msgType.infoStandard, $"{this.soiName}.{nameof(this.HandleBinaryRequest2)}()", this.msgCode, $"Request received in Sample Object Interceptor for {nameof(this.HandleBinaryRequest2)}");

            /*
             * Add code to manipulate requests here
             */

            IRequestHandler2 requestHandler = FindRequestHandlerDelegate<IRequestHandler2>();
            if (requestHandler != null)
            {
                return requestHandler.HandleBinaryRequest2(Capabilities, request);
            }

            //// Insert error response here.
            return null;
        }

        /// <summary>
        /// request soap xml
        /// </summary>
        /// <param name="Capabilities">list of capabilities</param>
        /// <param name="request">request soap</param>
        /// <returns>response soap/xml</returns>
        public string HandleStringRequest(string Capabilities, string request)
        {
            this.serverLog.LogMessage(ServerLogger.msgType.infoStandard, $"{this.soiName}.{nameof(this.HandleStringRequest)}()", this.msgCode, $"Request received in Sample Object Interceptor for {nameof(this.HandleStringRequest)}");

            /*
             * Add code to manipulate requests here
             */

            IRequestHandler requestHandler = FindRequestHandlerDelegate<IRequestHandler>();
            if (requestHandler != null)
            {
                return requestHandler.HandleStringRequest(Capabilities, request);
            }

            //// Insert error response here.
            return null;
        }

        #endregion

        #region Utility code

        /// <summary>
        /// Find Request Handler Delegate
        /// </summary>
        /// <typeparam name="THandlerInterface">Type Handler Interface</typeparam>
        /// <returns>Handler Interface</returns>
        private THandlerInterface FindRequestHandlerDelegate<THandlerInterface>() where THandlerInterface : class
        {
            try
            {
                IPropertySet props = this.ServerEnvironment.Properties;
                string extensionName;
                try
                {
                    extensionName = (string)props.GetProperty("ExtensionName");
                }
                catch (Exception /*e*/)
                {
                    extensionName = null;
                }

                if (string.IsNullOrEmpty(extensionName))
                {
                    return this.serverObjectHelper.ServerObject as THandlerInterface;
                }

                // Get the extension reference from cache if available
                if (this.extensionCache.ContainsKey(extensionName))
                {
                    return this.extensionCache[extensionName] as THandlerInterface;
                }

                // This request is to be made on a specific extension
                // so we find the extension from the extension manager
                IServerObjectExtensionManager extnMgr = this.serverObjectHelper.ServerObject as IServerObjectExtensionManager;
                IServerObjectExtension soe = extnMgr.FindExtensionByTypeName(extensionName);

                return soe as THandlerInterface;
            }
            catch (Exception e)
            {
                this.serverLog.LogMessage(ServerLogger.msgType.error, $"{this.soiName}.{nameof(this.FindRequestHandlerDelegate)}()", this.msgCode, e.ToString());
                throw;
            }
        }        
        #endregion
    }
}
