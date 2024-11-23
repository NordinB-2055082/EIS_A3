using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using Emgu.CV;
using Emgu.CV.CvEnum;
using System;
using System.Collections.Generic;
using System.Linq;

using System.Windows.Controls;

using Microsoft.Kinect;
using System.Drawing; 
using KinectGame.Calibration;
using System.Windows.Threading;

namespace KinectGame
{
    public partial class MainWindow : Window
    {
        private KinectSensor kinectSensor;
        private PartialCalibrationClass calibrationProcessor;
        private List<System.Windows.Point> screenPoints = new List<System.Windows.Point>();
        private Skeleton currentTrackedSkeleton;
        private int currentPointIndex = 0;
        private DispatcherTimer captureTimer;
        private bool isWaiting = true; // Start in "waiting" mode for the first point
        private bool isCalibrationComplete = false; // Flag to indicate calibration is complete

        public MainWindow()
        {
            InitializeComponent();
            InitializeKinect();
            SetupCalibrationPoints();
            SetupTimer();
            captureTimer.Start(); // Start timer for the first delay
        }

        private void InitializeKinect()
        {
            kinectSensor = KinectSensor.KinectSensors.FirstOrDefault(s => s.Status == KinectStatus.Connected);
            if (kinectSensor == null)
            {
                MessageBox.Show("No Kinect sensor connected.");
                Application.Current.Shutdown();
                return;
            }

            kinectSensor.SkeletonStream.Enable();
            kinectSensor.SkeletonFrameReady += KinectSkeletonFrameReady;
            kinectSensor.Start();

            calibrationProcessor = new PartialCalibrationClass(kinectSensor);
        }

        private void SetupCalibrationPoints()
        {
            // Define the corners of the calibration rectangle on the screen
            screenPoints.Add(new System.Windows.Point(200, 25));   // Top-left
            screenPoints.Add(new System.Windows.Point(600, 25));   // Top-right
            screenPoints.Add(new System.Windows.Point(600, 425));  // Bottom-right
            screenPoints.Add(new System.Windows.Point(200, 425));  // Bottom-left

            UpdateInstructions();
            MoveIndicator(screenPoints[currentPointIndex]);
        }

        private void SetupTimer()
        {
            captureTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5) // 5-second delay for all points, including the first
            };
            captureTimer.Tick += (sender, e) =>
            {
                isWaiting = false; // Allow point capture
                captureTimer.Stop();
            };
        }

        private void KinectSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            if (isCalibrationComplete) return; // Stop processing if calibration is complete

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame == null) return;

                Skeleton[] skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                skeletonFrame.CopySkeletonDataTo(skeletons);

                currentTrackedSkeleton = skeletons.FirstOrDefault(s => s.TrackingState == SkeletonTrackingState.Tracked);

                if (currentTrackedSkeleton != null && !isWaiting)
                {
                    CollectCalibrationPoint(screenPoints[currentPointIndex]);
                }
            }
        }

        private void CollectCalibrationPoint(System.Windows.Point screenPoint)
        {
            if (currentTrackedSkeleton == null || isCalibrationComplete) return;

            // Prevent immediate re-capture
            isWaiting = true;
            captureTimer.Start();

            // Add the skeleton and screen points to the calibration processor
            SkeletonPoint skeletonPoint = currentTrackedSkeleton.Position;
            calibrationProcessor.AddSkeletonCalibrationPoint(skeletonPoint);
            calibrationProcessor.AddCalibrationPoint(screenPoint);

            if (calibrationProcessor.HasAllPoints())
            {
                calibrationProcessor.calibrate();
                isCalibrationComplete = true; // Mark calibration as complete

                // Test the calibration result
                System.Windows.Point testProjection = calibrationProcessor.kinectToProjectionPoint(currentTrackedSkeleton.Position);
                InstructionsTextBlock.Text = $"Calibration Complete! Test Projection: ({testProjection.X}, {testProjection.Y})";
            }
            else
            {
                // Move to the next calibration point
                currentPointIndex++;
                if (currentPointIndex >= screenPoints.Count)
                {
                    InstructionsTextBlock.Text = "All calibration points have been captured.";
                    return; // Exit to prevent further processing
                }

                UpdateInstructions();
                MoveIndicator(screenPoints[currentPointIndex]);
            }
        }

        private void UpdateInstructions()
        {
            InstructionsTextBlock.Text = $"Stand in position {currentPointIndex + 1}.";
        }

        private void MoveIndicator(System.Windows.Point target)
        {
            Canvas.SetLeft(PointIndicator, target.X - PointIndicator.Width / 2);
            Canvas.SetTop(PointIndicator, target.Y - PointIndicator.Height / 2);
            PointIndicator.Visibility = Visibility.Visible;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (kinectSensor != null)
            {
                kinectSensor.Stop();
                kinectSensor.Dispose();
            }
        }
    }
}