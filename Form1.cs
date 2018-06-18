//----------------------------------------------------------------------------
//  Copyright (C) 2004-2016 by EMGU Corporation. All rights reserved.       
//----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Emgu.CV.VideoSurveillance;
using Emgu.CV.Cuda;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
namespace MotionDetection
{
    public partial class Form1 : Form
   {
        private Capture _capture;
        private double size = 10;
        private MotionHistory _motionHistory;
        private BackgroundSubtractor _forgroundDetector;
        private Mat motionmat = new Mat();
        private List<Bitmap> Availablebitmap = new List<Bitmap>();

        long detectionTime;
        bool tryUseCuda = true;
        System.IO.Ports.SerialPort com; //定义一个串口对象
      List<int> angel = new List<int>();
        public Form1()
      {
         InitializeComponent();

         //try to create the capture
         if (_capture == null)
         {
            try
            {
               _capture = new Capture();
                    
            }
            catch (NullReferenceException excpt)
            {   //show errors if there is any
               MessageBox.Show(excpt.Message);
            }
         }

         if (_capture != null) //if camera capture has been successfully created
         {
            _motionHistory = new MotionHistory(
                1.0, //in second, the duration of motion history you wants to keep
                0.05, //in second, maxDelta for cvCalcMotionGradient
                0.5); //in second, minDelta for cvCalcMotionGradient

            _capture.ImageGrabbed += ProcessFrame;
            _capture.Start();
         }
      }

      private Mat _segMask = new Mat();
      private Mat _forgroundMask = new Mat();
        private void ProcessFrame(object sender, EventArgs e)
        {
            Mat image = new Mat();
            _capture.Retrieve(image);
            if (_forgroundDetector == null)
            {
                _forgroundDetector = new BackgroundSubtractorMOG2();
            }

            _forgroundDetector.Apply(image, _forgroundMask);

            //update the motion history
            _motionHistory.Update(_forgroundMask);

            #region get a copy of the motion mask and enhance its color
            double[] minValues, maxValues;
            Point[] minLoc, maxLoc;
            _motionHistory.Mask.MinMax(out minValues, out maxValues, out minLoc, out maxLoc);
            Mat motionMask = new Mat();
            using (ScalarArray sa = new ScalarArray(255.0 / maxValues[0]))
                CvInvoke.Multiply(_motionHistory.Mask, sa, motionMask, 1, DepthType.Cv8U);
            //Image<Gray, Byte> motionMask = _motionHistory.Mask.Mul(255.0 / maxValues[0]);
            #endregion

            //DetectFace.Detect(image, "haarcascade_frontalface_default.xml", "haarcascade_eye.xml", faces, eyes, tryUseCuda, out detectionTime);
            //create the motion image 
            Mat motionImage = new Mat(motionMask.Size.Height, motionMask.Size.Width, DepthType.Cv8U, 3);
            //display the motion pixels in blue (first channel)
            //motionImage[0] = motionMask;
            CvInvoke.InsertChannel(motionMask, motionImage, 0);

            //Threshold to define a motion area, reduce the value to detect smaller motion
            double minArea = 100;

            //storage.Clear(); //clear the storage
            Rectangle[] rects;
            using (VectorOfRect boundingRect = new VectorOfRect())
            {
                _motionHistory.GetMotionComponents(_segMask, boundingRect);
                rects = boundingRect.ToArray();
            }
            List<Rectangle> Availablerects = new List<Rectangle>();
            foreach (Rectangle comp in rects)
            {
                int area = comp.Width * comp.Height;
                //reject the components that have small area;
                if (area < minArea) continue;
                double angle, motionPixelCount;
                _motionHistory.MotionInfo(_forgroundMask, comp, out angle, out motionPixelCount);
                //reject the area that contains too few motion
                if (motionPixelCount < area * size)
                {
                    continue;
                }
                else
                {
                    Availablerects.Add(comp);
                }
            }

            //iterate through each of the motion component
            List<Rectangle> faces = new List<Rectangle>();
            List<Rectangle> eyes = new List<Rectangle>();
            Task task2 = new Task(() =>
            {
                
                Mat Detectmat = new Mat();
                Detectmat = image;
                DetectFace.Detect(Detectmat, "haarcascade_frontalface_default.xml", "haarcascade_eye.xml", faces, eyes, tryUseCuda, out detectionTime);
                if (faces.Count > 0)
                {
                    
                    label1.Text = "detectionTime:" + detectionTime.ToString();
                    for (int i = 0; i < faces.Count; i++)
                    {
                        Bitmap bt2 = DetectFace.Cutbitmap(Detectmat.Bitmap, faces[i].X, faces[i].Y, faces[i].Width, faces[i].Height);
                        Emgu.CV.Image<Bgr, Byte> currentFrame1 = new Emgu.CV.Image<Bgr, Byte>(bt2);  //只能这么转
                        Mat invert1 = new Mat();
                        CvInvoke.BitwiseAnd(currentFrame1, currentFrame1, invert1);  //这是官网上的方法，变通用。没看到提供其它方法直接转换的。
                        faceimage.Image = invert1;
                        string filePath = "G:\\motion1\\" + DateTime.Now.ToString("人脸-yyyy年MM月dd日HH点mm分ss秒") + i.ToString() + "-" + faces.Count.ToString() + ".jpg";
                        bt2.Save(filePath);
                        System.Media.SystemSounds.Beep.Play();
                    }
                    Bitmap bt1 = Detectmat.Bitmap;
                    string filePath2 = "G:\\motion1\\" + DateTime.Now.ToString("原图-yyyy年MM月dd日HH点mm分ss秒") + ".jpg";
                    //System.Diagnostics.Debug.WriteLine("准备保存原图" + detectionTime.ToString());
                    bt1.Save(filePath2);

                }
               
            });
            task2.Start();
            
            foreach (Rectangle comp in Availablerects)
            {
                int area = comp.Width * comp.Height;
                //reject the components that have small area;
                if (area < minArea) continue;

              

                // find the angle and motion pixel count of the specific area
                double angle, motionPixelCount;
                _motionHistory.MotionInfo(_forgroundMask, comp, out angle, out motionPixelCount);

                //reject the area that contains too few motion
                if (motionPixelCount < area * size) continue;

                //Draw each individual motion in red

                //=================转换mat格式为bitmap并裁切=========================== 
                Task task = new Task(() =>
                {
                    Bitmap bt = DetectFace.Cutbitmap(image.Bitmap, comp.X, comp.Y, comp.Width, comp.Height);
                    Emgu.CV.Image<Bgr, Byte> currentFrame = new Emgu.CV.Image<Bgr, Byte>(bt);  //只能这么转
                    Mat invert = new Mat();
                    CvInvoke.BitwiseAnd(currentFrame, currentFrame, invert);  //这是官网上的方法，变通用。没看到提供其它方法直接转换的。
                    moveimage.Image = invert;
                });
                task.Start();
                try
                {
                    DrawMotion(motionImage, comp, angle, new Bgr(Color.Red));
                    DrawMotion(capturedImageBox.Image, comp, angle, new Bgr(Color.Red));
                }
                catch (Exception a)
                {

                }

                #region//area
                /*
                bool time = false;
                if ((comp.X > 1770 && comp.X < 1830) && (comp.Y > 2 && comp.Y < 40))
                {
                    time = true;
                }
                if (youxiaorects.Count < 50&&!time)
                {
                    if (capturedImageBox.Image != null)
                    {
                        Random rd = new Random();
                        Bitmap bt = new Bitmap(capturedImageBox.Image.Bitmap);
                     //   string filePath = "G:\\motion\\" + DateTime.Now.ToString("yyyy年MM月dd日HH点mm分ss秒") + ".jpg";
                     //   image.Save(filePath);

                    }
                }
                */
                #endregion
            }

            #region//垃圾堆
            //=================当检测到图像更变，获取更变区域坐标与大小时，尝试将更变区域保存 传入人脸识别函数分析=====================
            //===============根据更变区域个数来动态创建线程，增加效率======================
            /*   Thread[] downloadThread;
               Thread face=new Thread(confirmface);
               face.Start();*/
            /*  int areacount = Availablerects.Count;
              //声名下载线程，这是C#的优势，即数组初始化时，不需要指定其长度，可以在使用时才指定。

              //这个声名应为类级，这样也就为其它方法控件它们提供了可能

              ThreadStart startDownload = new ThreadStart(confirmface);
              //线程起始设置：即每个线程都执行DownLoad()
              downloadThread = new Thread[areacount];//为线程申请资源，确定线程总数
              for (int k = 0; k < areacount; k++)//开启指定数量的线程数
              {
                  downloadThread[k] = new Thread(startDownload);//指定线程起始设置
                  downloadThread[k].Start();//逐个开启线程
              }*/
            #endregion

            #region//_forgroundMask
           /* 
             // find and draw the overall motion angle
            double overallAngle, overallMotionPixelCount;

            _motionHistory.MotionInfo(_forgroundMask, new Rectangle(Point.Empty, motionMask.Size), out overallAngle, out overallMotionPixelCount);
            // DrawMotion(motionImage, new Rectangle(Point.Empty, motionMask.Size), overallAngle, new Bgr(Color.Green));
            // DrawMotion(image, new Rectangle(Point.Empty, image.Size), overallAngle, new Bgr(Color.Green));
            if (this.Disposing || this.IsDisposed)
                return;
                */
            /*  foreach (Rectangle face in faces)
                  CvInvoke.Rectangle(image, face, new Bgr(Color.Red).MCvScalar, 2);
              foreach (Rectangle eye in eyes)
                  CvInvoke.Rectangle(image, eye, new Bgr(Color.Blue).MCvScalar, 2);*/
            capturedImageBox.Image = image;
           // forgroundImageBox.Image = _forgroundMask;

            //Display the amount of motions found on the current image
            //UpdateText(String.Format("Total Motions found: {0}; Motion Pixel count: {1} detectionTime:{2} ", rects.Length, overallMotionPixelCount, detectionTime));

            //Display the image of the motion
          //  motionImageBox.Image = motionImage; 
           
            
            #endregion
        }
        private void UpdateText(String text)
      {
         if (!IsDisposed && !Disposing && InvokeRequired)
         {
            Invoke((Action<String>)UpdateText, text);
         }
         else
         {
            label3.Text = text;
         }
      }

      private static void DrawMotion(IInputOutputArray image, Rectangle motionRegion, double angle, Bgr color)
      {
         //CvInvoke.Rectangle(image, motionRegion, new MCvScalar(255, 255, 0));
         float circleRadius = (motionRegion.Width + motionRegion.Height) >> 2;
         Point center = new Point(motionRegion.X + (motionRegion.Width >> 1), motionRegion.Y + (motionRegion.Height >> 1));

         CircleF circle = new CircleF(
            center,
            circleRadius);

         int xDirection = (int)(Math.Cos(angle * (Math.PI / 180.0)) * circleRadius);
         int yDirection = (int)(Math.Sin(angle * (Math.PI / 180.0)) * circleRadius);
         Point pointOnCircle = new Point(
             center.X + xDirection,
             center.Y - yDirection);
         LineSegment2D line = new LineSegment2D(center, pointOnCircle);
         CvInvoke.Circle(image, Point.Round(circle.Center), (int)circle.Radius, color.MCvScalar);
         CvInvoke.Line(image, line.P1, line.P2, color.MCvScalar);

      }

      /// <summary>
      /// Clean up any resources being used.
      /// </summary>
      /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
      protected override void Dispose(bool disposing)
      {

         if (disposing && (components != null))
         {
            components.Dispose();
         }

         base.Dispose(disposing);
      }

      private void Form1_FormClosed(object sender, FormClosedEventArgs e)
      {
         _capture.Stop();
      }

        private void Form1_Load(object sender, EventArgs e)
        {
            Control.CheckForIllegalCrossThreadCalls = false;
        }

        private void trackBar2_ValueChanged(object sender, EventArgs e)
        {

            if (com != null)
                com.WriteLine("UD" + angel[Convert.ToInt16(trackBar2.Value)]);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string[] allport = null;
            try
            {
                allport = SerialPort.GetPortNames();
            }
            catch (Exception ex)
            {
            }
            for (int i = 180; i > 0; i--)
            {
                angel.Add(i);
               
            }
            if (com != null)
            {
                com.Close();
                com = null;
            }
            com = new System.IO.Ports.SerialPort(allport[allport.Length-1].ToString(), 9600);//初始化串口对象
            try
            {
                com.Open();
                trackBar1.Visible = true;
                trackBar1.Enabled = true;
                trackBar2.Visible = true;
                trackBar2.Enabled = true;
            }
            catch(Exception a)
            {
                MessageBox.Show(a.ToString());
            }
            if (com != null)
            {
                com.WriteLine("UD" + 90);
                Thread.Sleep(200);
                com.WriteLine("LR" + 90);
                com.WriteLine("B");
            }

          

        }

        private void trackBar1_ValueChanged(object sender, EventArgs e)
        {
            if (com != null)
                com.WriteLine("LR" + angel[Convert.ToInt16(trackBar1.Value)]);
        }
    }
}
