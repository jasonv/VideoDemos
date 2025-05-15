using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using AForge.Video;
using AForge.Video.DirectShow;

class Program
{
    // HTTP server that will stream the video to clients
    static HttpListener listener;

    // Video capture device (the laptop's webcam)
    static VideoCaptureDevice videoSource;

    // Holds the most recent frame captured from the camera
    static Bitmap latestFrame;

    static void Main()
    {
        // Set up a simple HTTP server on localhost:5000/video/
        listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:5000/video/");
        listener.Start();

        Console.WriteLine("Listening on http://localhost:5000/video/");

        // Start capturing video from the laptop's camera
        StartCamera();

        // Enter a loop to handle HTTP requests for the video stream
        while (true)
        {
            var context = listener.GetContext(); // Wait for a client connection
            ProcessRequest(context);            // Handle the client request
        }
    }

    static void StartCamera()
    {
        // Discover available video capture devices (e.g., webcams)
        var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
        if (videoDevices.Count == 0)
            throw new Exception("No camera found.");

        // Use the first available camera
        videoSource = new VideoCaptureDevice(videoDevices[0].MonikerString);

        // Register an event handler to receive frames as they are captured
        videoSource.NewFrame += new NewFrameEventHandler(Video_NewFrame);

        // Start the video capture
        videoSource.Start();
    }

    // This event is triggered each time a new frame is captured from the camera
    static void Video_NewFrame(object sender, NewFrameEventArgs eventArgs)
    {
        try
        {
            // Clone the frame to ensure it remains valid outside the event
            Bitmap frame = (Bitmap)eventArgs.Frame.Clone();

            // Dispose the previous frame to free up memory
            latestFrame?.Dispose();

            // Store the new frame as the latest frame
            latestFrame = frame;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Frame capture error: " + ex.Message);
        }
    }

    // Handles an HTTP request by continuously sending video frames in MJPEG format
    static void ProcessRequest(HttpListenerContext context)
    {
        // Set the response content type to multipart/x-mixed-replace for MJPEG streaming
        context.Response.ContentType = "multipart/x-mixed-replace; boundary=--frame";

        while (true)
        {
            // Skip if no frame has been captured yet
            if (latestFrame == null) continue;

            try
            {
                // Make a copy of the latest frame to avoid threading issues
                using (Bitmap frameCopy = new Bitmap(latestFrame))
                using (MemoryStream ms = new MemoryStream())
                {
                    // Convert the frame to JPEG format
                    frameCopy.Save(ms, ImageFormat.Jpeg);
                    byte[] imageBytes = ms.ToArray();

                    // Prepare the MJPEG boundary and headers
                    string header = "\r\n--frame\r\nContent-Type: image/jpeg\r\nContent-Length: " + imageBytes.Length + "\r\n\r\n";
                    byte[] headerBytes = Encoding.ASCII.GetBytes(header);

                    // Write the boundary, headers, and image data to the response stream
                    context.Response.OutputStream.Write(headerBytes, 0, headerBytes.Length);
                    context.Response.OutputStream.Write(imageBytes, 0, imageBytes.Length);
                    context.Response.OutputStream.Flush();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error sending frame: " + ex.Message);
            }

            // Control the frame rate (~10 frames per second)
            System.Threading.Thread.Sleep(100);
        }
    }
}
