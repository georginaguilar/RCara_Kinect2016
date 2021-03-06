﻿//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//Desde_0V3, captura expresiones faciales con las opciones deseadas, captura,UUID,
//inserta en la base de datos, se agrego un ciclo el cual funciona como temporizador
//para la captura y su insercion en la base de datos.
// 2DIC2015,SE AGREGO NOMBRE DE COMPUTADORA,CAMPO DE FECHA Y HORA, 
//FaceBasics_dsd_0V9 graba a todos los usuarios que detecta y coordenadas ala BD
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.FaceBasics
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Media.Media3D;
    using Microsoft.Kinect;
    using Microsoft.Kinect.Face;
    //mysql
    using MySql.Data.MySqlClient;
    using System.Windows.Threading;
 
    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// Thickness of face bounding box and face points
        /// </summary>
        private const double DrawFaceShapeThickness = 8;

        /// <summary>
        /// Font size of face property text 
        /// </summary>
        private const double DrawTextFontSize = 30;

        /// <summary>
        /// Radius of face point circle
        /// </summary>
        private const double FacePointRadius = 1.0;

        /// <summary>
        /// Text layout offset in X axis
        /// </summary>
        private const float TextLayoutOffsetX = -0.1f;
        
        /// <summary>
        /// Text layout offset in Y axis
        /// </summary>
        private const float TextLayoutOffsetY = -0.15f;

        /// <summary>
        /// Face rotation display angle increment in degrees
        /// </summary>
        private const double FaceRotationIncrementInDegrees = 5.0;

        /// <summary>
        /// Formatted text to indicate that there are no bodies/faces tracked in the FOV
        /// </summary>
        private FormattedText textFaceNotTracked = new FormattedText(
                        "No bodies or faces are tracked ...",
                        CultureInfo.GetCultureInfo("en-us"),
                        FlowDirection.LeftToRight,
                        new Typeface("Georgia"),
                        DrawTextFontSize,
                        Brushes.White);

        /// <summary>
        /// Text layout for the no face tracked message
        /// </summary>
        private Point textLayoutFaceNotTracked = new Point(10.0, 10.0);

        /// <summary>
        /// Drawing group for body rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Coordinate mapper to map one type of point to another
        /// </summary>
        private CoordinateMapper coordinateMapper = null;

        /// <summary>
        /// Reader for body frames
        /// </summary>
        private BodyFrameReader bodyFrameReader = null;

        /// <summary>
        /// Array to store bodies
        /// </summary>
        private Body[] bodies = null;

        /// <summary>
        /// Number of bodies tracked
        /// </summary>
        private int bodyCount;

        /// <summary>
        /// Face frame sources
        /// </summary>
        private FaceFrameSource[] faceFrameSources = null;

        /// <summary>
        /// Face frame readers
        /// </summary>
        private FaceFrameReader[] faceFrameReaders = null;

        /// <summary>
        /// Storage for face frame results
        /// </summary>
        private FaceFrameResult[] faceFrameResults = null;

        /// <summary>
        /// Width of display (color space)
        /// </summary>
        private int displayWidth;

        /// <summary>
        /// Height of display (color space)
        /// </summary>
        private int displayHeight;

        /// <summary>
        /// Display rectangle
        /// </summary>
        private Rect displayRect;

        /// <summary>
        /// List of brushes for each face tracked
        /// </summary>
        private List<Brush> faceBrush;

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string statusText = null;

        //UUID(ID_KINECT)
        private string strGuid;

        private int counter = 0;

        //fecha
        private string fecha; 
        
        //nombre_pc
        private string nombrePC;

        ////color de cara_referencia
        //private string color_f;

        //conex a base de datos
        FaceBasics.connection conn = new FaceBasics.connection();

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            // one sensor is currently supported
            this.kinectSensor = KinectSensor.GetDefault();

            // get the coordinate mapper
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            // get the color frame details
            FrameDescription frameDescription = this.kinectSensor.ColorFrameSource.FrameDescription;

            // set the display specifics
            this.displayWidth = frameDescription.Width;
            this.displayHeight = frameDescription.Height;
            this.displayRect = new Rect(0.0, 0.0, this.displayWidth, this.displayHeight);

            // open the reader for the body frames
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();

            // wire handler for body frame arrival
            this.bodyFrameReader.FrameArrived += this.Reader_BodyFrameArrived;

            // set the maximum number of bodies that would be tracked by Kinect
            this.bodyCount = this.kinectSensor.BodyFrameSource.BodyCount;

            // allocate storage to store body objects
            this.bodies = new Body[this.bodyCount];

            // specify the required face frame results
            FaceFrameFeatures faceFrameFeatures =
                FaceFrameFeatures.BoundingBoxInColorSpace
                | FaceFrameFeatures.PointsInColorSpace
                | FaceFrameFeatures.RotationOrientation
                | FaceFrameFeatures.FaceEngagement
                | FaceFrameFeatures.Glasses
                | FaceFrameFeatures.Happy
                | FaceFrameFeatures.LeftEyeClosed
                | FaceFrameFeatures.RightEyeClosed
                | FaceFrameFeatures.LookingAway
                | FaceFrameFeatures.MouthMoved
                | FaceFrameFeatures.MouthOpen;

            // create a face frame source + reader to track each face in the FOV
            this.faceFrameSources = new FaceFrameSource[this.bodyCount];
            this.faceFrameReaders = new FaceFrameReader[this.bodyCount];
            for (int i = 0; i < this.bodyCount; i++)
            {
                // create the face frame source with the required face frame features and an initial tracking Id of 0
                this.faceFrameSources[i] = new FaceFrameSource(this.kinectSensor, 0, faceFrameFeatures);

                // open the corresponding reader
                this.faceFrameReaders[i] = this.faceFrameSources[i].OpenReader();
            }

            // allocate storage to store face frame results for each face in the FOV
            this.faceFrameResults = new FaceFrameResult[this.bodyCount];

            // populate face result colors - one for each face index
            this.faceBrush = new List<Brush>()
            {
                Brushes.White, 
                Brushes.Orange,
                Brushes.Green,
                Brushes.Red,
                Brushes.LightBlue,
                Brushes.Yellow
            };

            //List<Brush> faceBrush;

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.NoSensorStatusText;

            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // use the window object as the view model in this simple example
            this.DataContext = this;

            // initialize the components (controls) of the window
            this.InitializeComponent();

            //Generar Clave Unica por evento
            string clave_unica;

            strGuid = System.Guid.NewGuid().ToString().ToUpper();
            clave_unica = String.Format(strGuid);
            claveUnica.Content = clave_unica;
            
         //fecha
            fecha = DateTimeOffset.Now.ToString("MM/dd/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            time.Content = fecha;
        
        //nombre_pc
        nombrePC=Environment.MachineName;
        nom_pc.Content= nombrePC;

        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                return this.imageSource;
            }
        }

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        /// Converts rotation quaternion to Euler angles 
        /// And then maps them to a specified range of values to control the refresh rate
        /// </summary>
        /// <param name="rotQuaternion">face rotation quaternion</param>
        /// <param name="pitch">rotation about the X-axis</param>
        /// <param name="yaw">rotation about the Y-axis</param>
        /// <param name="roll">rotation about the Z-axis</param>
        private static void ExtractFaceRotationInDegrees(Vector4 rotQuaternion, out int pitch, out int yaw, out int roll)
        {
            double x = rotQuaternion.X;
            double y = rotQuaternion.Y;
            double z = rotQuaternion.Z;
            double w = rotQuaternion.W;

            // convert face rotation quaternion to Euler angles in degrees
            double yawD, pitchD, rollD;
            pitchD = Math.Atan2(2 * ((y * z) + (w * x)), (w * w) - (x * x) - (y * y) + (z * z)) / Math.PI * 180.0;
            yawD = Math.Asin(2 * ((w * y) - (x * z))) / Math.PI * 180.0;
            rollD = Math.Atan2(2 * ((x * y) + (w * z)), (w * w) + (x * x) - (y * y) - (z * z)) / Math.PI * 180.0;

            // clamp the values to a multiple of the specified increment to control the refresh rate
            double increment = FaceRotationIncrementInDegrees;
            pitch = (int)(Math.Floor((pitchD + ((increment / 2.0) * (pitchD > 0 ? 1.0 : -1.0))) / increment) * increment);
            yaw = (int)(Math.Floor((yawD + ((increment / 2.0) * (yawD > 0 ? 1.0 : -1.0))) / increment) * increment);
            roll = (int)(Math.Floor((rollD + ((increment / 2.0) * (rollD > 0 ? 1.0 : -1.0))) / increment) * increment);
        }

        /// <summary>
        /// Execute start up tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < this.bodyCount; i++)
            {
                if (this.faceFrameReaders[i] != null)
                {
                    // wire handler for face frame arrival
                    this.faceFrameReaders[i].FrameArrived += this.Reader_FaceFrameArrived;               
                }
            }

            if (this.bodyFrameReader != null)
            {
                // wire handler for body frame arrival
                this.bodyFrameReader.FrameArrived += this.Reader_BodyFrameArrived;
                //no_cuerpos.Content = this.bodies[bodyCount];
            }
         }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            for (int i = 0; i < this.bodyCount; i++)
            {
                if (this.faceFrameReaders[i] != null)
                {
                    // FaceFrameReader is IDisposable
                    this.faceFrameReaders[i].Dispose();
                    this.faceFrameReaders[i] = null;
                }

                if (this.faceFrameSources[i] != null)
                {
                    // FaceFrameSource is IDisposable
                    this.faceFrameSources[i].Dispose();
                    this.faceFrameSources[i] = null;
                }
            }
            if (this.bodyFrameReader != null)
            {
                // BodyFrameReader is IDisposable
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        /// <summary>
        /// Handles the face frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_FaceFrameArrived(object sender, FaceFrameArrivedEventArgs e)
        {
            using (FaceFrame faceFrame = e.FrameReference.AcquireFrame())
            {
                if (faceFrame != null)
                {
                    // get the index of the face source from the face source array
                    int index = this.GetFaceSourceIndex(faceFrame.FaceFrameSource);

                    // check if this face frame has valid face frame results
                    if (this.ValidateFaceBoxAndPoints(faceFrame.FaceFrameResult))
                    {
                        // store this face frame result to draw later
                        this.faceFrameResults[index] = faceFrame.FaceFrameResult;
                    }
                    else
                    {
                        // indicates that the latest face frame result from this reader is invalid
                        this.faceFrameResults[index] = null;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the index of the face frame source
        /// </summary>
        /// <param name="faceFrameSource">the face frame source</param>
        /// <returns>the index of the face source in the face source array</returns>
        private int GetFaceSourceIndex(FaceFrameSource faceFrameSource)
        {
            int index = -1;

            for (int i = 0; i < this.bodyCount; i++)
            {
                if (this.faceFrameSources[i] == faceFrameSource)
                {
                    index = i;
                    break;
                }
            }

            return index;
        }

        /// <summary>
        /// Handles the body frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_BodyFrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            if (counter % 30 == 0) //cuenta cada 30 frames a cuantos usuarios hay presentes y recolecta los datos para guardarlas en la base de datos
            {
                using (var bodyFrame = e.FrameReference.AcquireFrame())
                {
                    if (bodyFrame != null)
                    {
                        // update body data
                        bodyFrame.GetAndRefreshBodyData(this.bodies);

                        using (DrawingContext dc = this.drawingGroup.Open())
                        {
                            // draw the dark background
                            dc.DrawRectangle(Brushes.Black, null, this.displayRect);

                            bool drawFaceResult = false;
                            
                            // iterate through each face source
                            for (int i = 0; i < this.bodyCount; i++)
                            {
                                // check if a valid face is tracked in this face source
                                if (this.faceFrameSources[i].IsTrackingIdValid)
                                {
                                    // check if we have valid face frame results
                                    if (this.faceFrameResults[i] != null)
                                    {
                                        // draw face frame results
                                        this.DrawFaceFrameResults(i, this.faceFrameResults[i], dc);

                                        if (!drawFaceResult)
                                        {
                                            drawFaceResult = true;
                                        }
                                    }
                                }
                                else
                                {
                                    // check if the corresponding body is tracked 
                                    if (this.bodies[i].IsTracked)
                                    {
                                        // update the face frame source to track this body
                                        this.faceFrameSources[i].TrackingId = this.bodies[i].TrackingId;
                                    }
                                }
                                resource_id.Content = this.faceFrameSources[0].FaceFrameFeatures;
                             }

                            if (!drawFaceResult)
                            {
                                // if no faces were drawn then this indicates one of the following:
                                // a body was not tracked 
                                // a body was tracked but the corresponding face was not tracked
                                // a body and the corresponding face was tracked though the face box or the face points were not valid
                                dc.DrawText(
                                    this.textFaceNotTracked,
                                    this.textLayoutFaceNotTracked);
                            }

                            this.drawingGroup.ClipGeometry = new RectangleGeometry(this.displayRect);
                        }
                    }
                }
            }
             counter++;
        }

        /// <summary>
        /// Draws face frame results
        /// </summary>
        /// <param name="faceIndex">the index of the face frame corresponding to a specific body in the FOV</param>
        /// <param name="faceResult">container of all face frame results</param>
        /// <param name="drawingContext">drawing context to render to</param>
        private void DrawFaceFrameResults(int faceIndex, FaceFrameResult faceResult, DrawingContext drawingContext)
        {
            // choose the brush based on the face index
            Brush drawingBrush = this.faceBrush[0];
            no_cuerpos.Content = faceIndex.ToString();

            if (faceIndex < this.bodyCount)
            {
                drawingBrush = this.faceBrush[faceIndex];
               
                //muestra el color de la cara en hexadecimal
                ColorCara.Content = faceBrush[faceIndex].ToString();
            }

            Pen drawingPen = new Pen(drawingBrush, DrawFaceShapeThickness);

            // draw the face bounding box
            var faceBoxSource = faceResult.FaceBoundingBoxInColorSpace;

//distancia entre dos puntos
            //float distancia = Math.Sqrt(Math.Pow(x2-x1,2)+Math.Pow(y2-y1,2));

            Rect faceBox = new Rect(faceBoxSource.Left, faceBoxSource.Top, faceBoxSource.Right - faceBoxSource.Left, faceBoxSource.Bottom - faceBoxSource.Top);
            drawingContext.DrawRectangle(null, drawingPen, faceBox);

            //resta para sacar diagonal , es decir centro
            //float resta1 = faceBoxSource.Right - faceBoxSource.Left;
            //float resta2 = faceBoxSource.Bottom - faceBoxSource.Top;
            //float a=faceBoxSource.Left;
            //float b=faceBoxSource.Right;
            //float c=faceBoxSource.Bottom;
            //float d=faceBoxSource.Top;

            //distancia entre dos puntos
            //float distancia = Math.Sqrt(Math.Pow(x2-x1,2)+Math.Pow(y2-y1,2));
            double distanciaZ;

           distanciaZ = Math.Sqrt(Math.Pow(faceBox.Bottom - faceBox.Top, 2) + Math.Pow(faceBoxSource.Right - faceBoxSource.Left, 2));
            

            if (faceResult.FacePointsInColorSpace != null)
            {
                // draw each face point
                foreach (PointF pointF in faceResult.FacePointsInColorSpace.Values)
                {
                    drawingContext.DrawEllipse(null, drawingPen, new Point(pointF.X, pointF.Y), FacePointRadius, FacePointRadius);
                }
            }

            string faceText = string.Empty;

            // extract each face property information and store it in faceText
            if (faceResult.FaceProperties != null)
            {
                foreach (var item in faceResult.FaceProperties)
                {
                    faceText += item.Key.ToString() + " : ";

                    // consider a "maybe" as a "no" to restrict 
                    // the detection result refresh rate
                    if (item.Value == DetectionResult.Maybe)
                    {
                        faceText += DetectionResult.No + "\n";
                    }
                    else
                    {
                        faceText += item.Value.ToString() + "\n";
                    }                    
                }
            }

            // extract face rotation in degrees as Euler angles
            if (faceResult.FaceRotationQuaternion != null)
            {
                int pitch, yaw, roll;
                ExtractFaceRotationInDegrees(faceResult.FaceRotationQuaternion, out pitch, out yaw, out roll);
                faceText += "FaceYaw : " + yaw + "\n" +
                            "FacePitch : " + pitch + "\n" +
                            "FacenRoll : " + roll + "\n";
            }

            // render the face property and face rotation information
            Point faceTextLayout;
            if (this.GetFaceTextPositionInColorSpace(faceIndex, out faceTextLayout))
            {
                drawingContext.DrawText(
                        new FormattedText(
                            faceText,
                            CultureInfo.GetCultureInfo("en-us"),
                            FlowDirection.LeftToRight,
                            new Typeface("Georgia"),
                            DrawTextFontSize,
                            drawingBrush),
                        faceTextLayout);
            }
            FaceFrameResult cara = faceResult;

            MySqlCommand command = new MySqlCommand();
            command.CommandType = System.Data.CommandType.Text;

            command.CommandText = "INSERT INTO extradata_capturev9_videos(clave_unica,Happy,Engage,WearingGlasses,LeftEyeClosed,RightEyeClosed,MouthOpen,MouthMoved,LookingAway,nombrePC,No_Usuarios,FaceBoxSourceLeft,FaceBoxSourceTop,FaceBoxSourceRight,FaceBoxSourceBottom,PointFX,PointFY,distanciaZ) VALUES (@clave_unica,@Happy,@Engaged,@WearingGlasses,@LeftEyeClosed,@RightEyeClosed,@MouthOpen,@MouthMoved,@LookingAway,@nombrePC,@No_Usuarios,@FaceBoxSourceLeft,@FaceBoxSourceTop,@FaceBoxSourceRight,@FaceBoxSourceBottom,@PointFX,@PointFY,@distanciaZ);";
           
            command.Parameters.Add("@clave_unica", MySqlDbType.VarChar, 200);
            command.Parameters["@clave_unica"].Value = strGuid;

            command.Parameters.Add("@Happy", MySqlDbType.VarChar, 10);
            command.Parameters["@Happy"].Value = cara.FaceProperties[FaceProperty.Happy].ToString();

            command.Parameters.Add("@Engaged", MySqlDbType.VarChar, 10);
            command.Parameters["@Engaged"].Value = cara.FaceProperties[FaceProperty.Engaged].ToString();

            command.Parameters.Add("@WearingGlasses", MySqlDbType.VarChar, 10);
            command.Parameters["@WearingGlasses"].Value = cara.FaceProperties[FaceProperty.WearingGlasses].ToString();

            command.Parameters.Add("@LeftEyeClosed", MySqlDbType.VarChar, 10);
            command.Parameters["@LeftEyeClosed"].Value = cara.FaceProperties[FaceProperty.LeftEyeClosed].ToString();

            command.Parameters.Add("@RightEyeClosed", MySqlDbType.VarChar, 10);
            command.Parameters["@RightEyeClosed"].Value = cara.FaceProperties[FaceProperty.RightEyeClosed].ToString();

            command.Parameters.Add("@MouthOpen", MySqlDbType.VarChar, 10);
            command.Parameters["@MouthOpen"].Value = cara.FaceProperties[FaceProperty.MouthOpen].ToString();

            command.Parameters.Add("@MouthMoved", MySqlDbType.VarChar, 10);
            command.Parameters["@MouthMoved"].Value = cara.FaceProperties[FaceProperty.MouthMoved].ToString();

            command.Parameters.Add("@LookingAway", MySqlDbType.VarChar, 10);
            command.Parameters["@LookingAway"].Value = cara.FaceProperties[FaceProperty.LookingAway].ToString();

            command.Parameters.Add("@No_Usuarios", MySqlDbType.VarChar,10);
            command.Parameters["@No_Usuarios"].Value = faceIndex;

            command.Parameters.Add("@nombrePC", MySqlDbType.VarChar, 20);
            command.Parameters["@nombrePC"].Value = nombrePC;

            //extras para saber cerca o lejos estan de la obre
            command.Parameters.Add("@FaceBoxSourceLeft", MySqlDbType.Float);//base de datos
            command.Parameters["@FaceBoxSourceLeft"].Value = faceBoxSource.Left;// variable a mandar a bd refaceBoxSource.Left;

            command.Parameters.Add("@FaceBoxSourceTop", MySqlDbType.Float);
            command.Parameters["@FaceBoxSourceTop"].Value = faceBoxSource.Top;

            command.Parameters.Add("@FaceBoxSourceRight", MySqlDbType.Float);
            command.Parameters["@FaceBoxSourceRight"].Value = faceBox.Right;

            command.Parameters.Add("@FaceBoxSourceBottom", MySqlDbType.Float);
            command.Parameters["@FaceBoxSourceBottom"].Value = faceBox.Bottom;

            //puntos en X y Y
            command.Parameters.Add("@PointFX", MySqlDbType.Float);
            command.Parameters["@PointFX"].Value = faceBox.X;

            command.Parameters.Add("@PointFY", MySqlDbType.Float);
            command.Parameters["@PointFY"].Value = faceBox.Y;

            //resta entre puntos distancia
            command.Parameters.Add("@distanciaZ",MySqlDbType.Float);
            command.Parameters["@distanciaZ"].Value = distanciaZ;

            //FaceBox.Z
            //command.Parameters.Add("@Z", MySqlDbType.Float);
            //command.Parameters["@Z"].Value = this.faceFrameSources[0].FaceFrameFeatures; 


            //command.Parameters.Add("@Resource_Id", MySqlDbType.VarChar, 10);
            //command.Parameters["@Resource_Id"].Value = txt_resource_Id.Text;

            command.Connection = conn.conection;
            conn.connect();
            command.ExecuteNonQuery();
            conn.desconectar();
        }

        private object DateTime()
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// Computes the face result text position by adding an offset to the corresponding 
        /// body's head joint in camera space and then by projecting it to screen space
        /// </summary>
        /// <param name="faceIndex">the index of the face frame corresponding to a specific body in the FOV</param>
        /// <param name="faceTextLayout">the text layout position in screen space</param>
        /// <returns>success or failure</returns>
        private bool GetFaceTextPositionInColorSpace(int faceIndex, out Point faceTextLayout)
        {
            faceTextLayout = new Point();
            bool isLayoutValid = false;

            Body body = this.bodies[faceIndex];
            if (body.IsTracked)
            {
                var headJoint = body.Joints[JointType.Head].Position;
                //agregado plano en z
                //resource_id.Content = headJoint.Z;
                //resource_id.Content = body.Joints[JointType.Head].Position.Z;

                CameraSpacePoint textPoint = new CameraSpacePoint()
                {
                    X = headJoint.X + TextLayoutOffsetX,
                    Y = headJoint.Y + TextLayoutOffsetY,
                    Z = headJoint.Z
                };

                resource_id2.Content = headJoint.Z;
                ColorSpacePoint textPointInColor = this.coordinateMapper.MapCameraPointToColorSpace(textPoint);

                faceTextLayout.X = textPointInColor.X;
                faceTextLayout.Y = textPointInColor.Y;
                isLayoutValid = true;                
            }

            return isLayoutValid;
        }

        /// <summary>
        /// Validates face bounding box and face points to be within screen space
        /// </summary>
        /// <param name="faceResult">the face frame result containing face box and points</param>
        /// <returns>success or failure</returns>
        private bool ValidateFaceBoxAndPoints(FaceFrameResult faceResult)
        {
            bool isFaceValid = faceResult != null;

            if (isFaceValid)
            {
                var faceBox = faceResult.FaceBoundingBoxInColorSpace;
                if (faceBox != null)
                {
                    // check if we have a valid rectangle within the bounds of the screen space
                    isFaceValid = (faceBox.Right - faceBox.Left) > 0 &&
                                  (faceBox.Bottom - faceBox.Top) > 0 &&
                                  faceBox.Right <= this.displayWidth &&
                                  faceBox.Bottom <= this.displayHeight;

                    if (isFaceValid)
                    {
                        var facePoints = faceResult.FacePointsInColorSpace;
                        if (facePoints != null)
                        {
                            foreach (PointF pointF in facePoints.Values)
                            {
                                // check if we have a valid face point within the bounds of the screen space
                                bool isFacePointValid = pointF.X > 0.0f &&
                                                        pointF.Y > 0.0f &&
                                                        pointF.X < this.displayWidth &&
                                                        pointF.Y < this.displayHeight;

                                if (!isFacePointValid)
                                {
                                    isFaceValid = false;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            return isFaceValid;
        }

        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            if (this.kinectSensor != null)
            {
                // on failure, set the status text
                this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                                : Properties.Resources.SensorNotAvailableStatusText;
            }
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

            //agregar numero de textbox
            //command.Parameters.Add("@Resource_Id", MySqlDbType.VarChar, 10);
            //command.Parameters["@Resource_Id"].Value = txt_resource_Id.Text;
       
    }
}
