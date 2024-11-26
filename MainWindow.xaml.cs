using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Microsoft.Kinect;
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
        private Skeleton secondTrackedSkeleton; // Added for second player
        private int currentPointIndex = 0;
        private DispatcherTimer captureTimer;
        private bool isWaiting = true; // Start in "waiting" mode for the first point
        private bool isCalibrationComplete = false; // Flag to indicate calibration is complete
        private List<Line> drawnLinesPlayer1 = new List<Line>(); // Store drawn lines for player 1
        private List<Line> drawnLinesPlayer2 = new List<Line>(); // Store drawn lines for player 2
        private GestureDetector gestureDetector;

        public MainWindow()
        {
            InitializeComponent();
            InitializeKinect();
            SetupCalibrationPoints();
            SetupTimer();
            captureTimer.Start(); // Start timer for the first delay

            // Initialize the gesture detector
            gestureDetector = new GestureDetector();
            gestureDetector.OnPaintGesture += DrawAt;
            gestureDetector.OnEraseGesture += EraseDrawing;
        }

        private void InitializeKinect()
        {
            kinectSensor = KinectSensor.KinectSensors.FirstOrDefault(s => s.Status == KinectStatus.Connected);
            if (kinectSensor == null)
            {
                MessageBox.Show("No Kinect sensor connected.");
                System.Windows.Application.Current.Shutdown();
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
            screenPoints.Add(new System.Windows.Point(1000, 25));   // Top-right
            screenPoints.Add(new System.Windows.Point(1000, 625));  // Bottom-right
            screenPoints.Add(new System.Windows.Point(200, 625));  // Bottom-left

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
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame == null) return;

                Skeleton[] skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                skeletonFrame.CopySkeletonDataTo(skeletons);

                currentTrackedSkeleton = skeletons.FirstOrDefault(s => s.TrackingState == SkeletonTrackingState.Tracked);
                secondTrackedSkeleton = skeletons.Skip(1).FirstOrDefault(s => s.TrackingState == SkeletonTrackingState.Tracked);

                if (currentTrackedSkeleton != null && !isWaiting)
                {
                    CollectCalibrationPoint(screenPoints[currentPointIndex]);
                    if (isCalibrationComplete)
                    {
                        System.Windows.Point projectedPoint1 = calibrationProcessor.kinectToProjectionPoint(currentTrackedSkeleton.Position);
                        UpdateUser(projectedPoint1, 1); // Update for player 1

                        // Update the gesture detector
                        gestureDetector.Update(currentTrackedSkeleton, secondTrackedSkeleton);
                    }
                }

                if (secondTrackedSkeleton != null && !isWaiting)
                {
                    System.Windows.Point projectedPoint2 = calibrationProcessor.kinectToProjectionPoint(secondTrackedSkeleton.Position);
                    UpdateUser(projectedPoint2, 2); // Update for player 2
                }
            }
        }

        private void UpdateUser(System.Windows.Point projectedPoint, int player)
        {
            // Round the projected point coordinates to 1 decimal place
            double roundedX = Math.Round(projectedPoint.X, 0);
            double roundedY = Math.Round(projectedPoint.Y, 0);
            PointIndicator.Visibility = Visibility.Collapsed;

            // Move a visual indicator to the user's projected position
            if (player == 1)
            {
                User_Indicator1.Visibility = Visibility.Visible;
                Canvas.SetLeft(User_Indicator1, roundedX - User_Indicator1.Width / 2);
                Canvas.SetTop(User_Indicator1, roundedY - User_Indicator1.Height / 2);
                User_PositionTextBlock1.Text = $"Player 1 Position: ({roundedX}, {roundedY})";
            }
            else if (player == 2)
            {
                User_Indicator2.Visibility = Visibility.Visible;
                Canvas.SetLeft(User_Indicator2, roundedX - User_Indicator2.Width / 2);
                Canvas.SetTop(User_Indicator2, roundedY - User_Indicator2.Height / 2);
                User_PositionTextBlock2.Text = $"Player 2 Position: ({roundedX}, {roundedY})";
                
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
                InstructionsTextBlock.Text = "All calibration points have been captured.";
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

        private void DrawAt(SkeletonPoint position, System.Windows.Media.Color color)
        {
            // Convert the skeleton position to screen coordinates
            System.Windows.Point screenPoint = calibrationProcessor.kinectToProjectionPoint(position);
            Line line = new Line
            {
                Stroke = new SolidColorBrush(color), // Use the color parameter
                StrokeThickness = 20,
                X1 = Math.Round(screenPoint.X),
                Y1 = Math.Round(screenPoint.Y),
                X2 = Math.Round(screenPoint.X) + 1, // Draw a small line segment
                Y2 = Math.Round(screenPoint.Y) + 1,
            };
            CalibrationCanvas.Children.Add(line);

            // Store lines in the appropriate list based on the color
            if (color == Colors.Black)
                drawnLinesPlayer1.Add(line);
            else if (color == Colors.Red)
                drawnLinesPlayer2.Add(line);
        }

        private void EraseDrawing(System.Windows.Media.Color color)
        {
            if (color == Colors.Black)
            {
                // Clear lines for player 1
                foreach (var line in drawnLinesPlayer1)
                {
                    CalibrationCanvas.Children.Remove(line);
                }
                drawnLinesPlayer1.Clear();
            }
            else if (color == Colors.Red)
            {
                // Clear lines for player 2
                foreach (var line in drawnLinesPlayer2)
                {
                    CalibrationCanvas.Children.Remove(line);
                }
                drawnLinesPlayer2.Clear();

            }

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