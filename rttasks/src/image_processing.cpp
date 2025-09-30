#include "image_processing.h"

#include <opencv2/opencv.hpp>

#include "camera_helpers.h" // For image constants

using namespace cv;

namespace ImageProcessing
{
  void SubsampleBayer(const Mat& in, Mat& out)
  {
    // Subsample the Bayer image by taking every second 2x2 pixel block
    for (int y = 0; y < CameraHelpers::IMAGE_HEIGHT; y += 4) {
      const uchar* const row0 = in.ptr<uchar>(y);
      const uchar* const row1 = in.ptr<uchar>(y + 1);

      uchar* outRow0 = out.ptr<uchar>(y / 2);
      uchar* outRow1 = out.ptr<uchar>(y / 2 + 1);

      for (int x = 0; x < CameraHelpers::IMAGE_WIDTH; x += 4)
      {
        outRow0[x / 2    ] = row0[x    ];
        outRow0[x / 2 + 1] = row0[x + 1];
        outRow1[x / 2    ] = row1[x    ];
        outRow1[x / 2 + 1] = row1[x + 1];
      }
    }
  }

  void SubsampleYUYV(const Mat& in, Mat& out)
  {
    for (int i = 0; i < CameraHelpers::IMAGE_HEIGHT; i += 2)
    {
      const uchar* const inRow = in.ptr<uchar>(i);    // 2 channels per pixel
      uchar* outRow = out.ptr<uchar>(i / 2);          // 3 channels per pixel

      for (int j = 0; j < CameraHelpers::IMAGE_WIDTH; j += 2)
      {
        outRow[3 * (j / 2)    ] = inRow[2 * j    ];      // channel 0 of pixel j (Y)
        outRow[3 * (j / 2) + 1] = inRow[2 * j + 1];      // channel 1 of pixel j (U)
        outRow[3 * (j / 2) + 2] = inRow[2 * j + 3];      // channel 1 of pixel j+1 (V)
      }
    }
  }

  void ExtractV(const Mat& in, Mat& out)
  {
    // Extract the V channel from a YUV image
    for (int i = 0; i < CameraHelpers::IMAGE_HEIGHT; ++i)
    {
      const uchar* const inRow = in.ptr<uchar>(i);    // 2 channels per pixel
      uchar* outRow = out.ptr<uchar>(i / 2);

      for (int j = 0; j < CameraHelpers::IMAGE_WIDTH; j += 2)
      {
        outRow[j / 2] = inRow[2 * j + 3];      // channel 1 of pixel j+1 (V)
      }
    }
  }

  void MaskV(const Mat& in, Mat& out)
  {
    static Mat kernel = getStructuringElement(MORPH_ELLIPSE, Size(7, 7));

    threshold(in, out, RED_THRESHOLD, 255, THRESH_BINARY);
    morphologyEx(out, out, MORPH_CLOSE, kernel);
    morphologyEx(out, out, MORPH_OPEN, kernel);
  }

  double CircleFitError(const std::vector<cv::Point>& pts, const cv::Point2f& center, float radius)
  {
    double sum = 0.0;
    for (const auto& p : pts) {
      double d = cv::norm(center - cv::Point2f(p));
      sum += (d - radius) * (d - radius);
    }
    return pts.empty() ? 0.0 : sum / pts.size();
  }

  void FitCircleTaubin(const std::vector<cv::Point> &pts, cv::Point2f &center, float &radius)
  {
    // Taubin fit method for circle fitting (Newton-based) : https://people.cas.uab.edu/~mosya/cl/MATLABcircle.html
    constexpr int MAX_ITERS = 10;
    constexpr double EPSILON = 1e-12;
    
    const size_t numPoints = pts.size();

    double sum_x = 0, sum_y = 0;
    for (const auto &p : pts) {
      sum_x += p.x;
      sum_y += p.y;
    }
    double mean_x = sum_x / numPoints;
    double mean_y = sum_y /numPoints;

    // Center data
    std::vector<cv::Point2f> cpts;
    cpts.reserve(numPoints);
    for (const auto &p : pts) cpts.emplace_back(p.x - mean_x, p.y - mean_y);

    // Compute moments
    double Mxx=0, Myy=0, Mxy=0, Mxz=0, Myz=0, Mzz=0;
    for (const auto &p : cpts) {
      double x = p.x, y = p.y;
      double z = x*x + y*y;
      Mxx += x*x;
      Myy += y*y;
      Mxy += x*y;
      Mxz += x*z;
      Myz += y*z;
      Mzz += z*z;
    }
    Mxx /= numPoints; Myy /= numPoints; Mxy /= numPoints;
    Mxz /= numPoints; Myz /= numPoints; Mzz /= numPoints;

    // Taubin’s eigenproblem coefficients
    double Mz = Mxx + Myy;
    double Cov_xy = Mxx*Myy - Mxy*Mxy;
    double A3 = 4*Mz;
    double A2 = -3*Mz*Mz - Mzz;
    double A1 = Mzz*Mz + 4*Cov_xy*Mz - Mxz*Mxz - Myz*Myz;
    double A0 = Mxz*Mxz*Myy + Myz*Myz*Mxx - Mzz*Cov_xy - 2*Mxz*Myz*Mxy;
    double xnew = 0;

    for (int i = 0; i < MAX_ITERS; ++i) {
      double y = A3*xnew*xnew*xnew + A2*xnew*xnew + A1*xnew + A0;
      double Dy = 3*A3*xnew*xnew + 2*A2*xnew + A1;
      double xold = xnew;
      xnew = xold - y/Dy;
      if (fabs((xnew - xold)/xnew) < EPSILON) break;
    }

    double det = xnew*xnew + xnew*Mz + Cov_xy;
    double a = (Mxz*(Myy - xnew) - Myz*Mxy) / det / 2.0;
    double b = (Myz*(Mxx - xnew) - Mxz*Mxy) / det / 2.0;
    double r = sqrt(a*a + b*b + (Mz + xnew));

    center = cv::Point2f(static_cast<float>(a + mean_x), static_cast<float>(b + mean_y));
    radius = static_cast<float>(r);
  }

  void FitCircleLeastSquares(const std::vector<Point> &contour, Point2f &center, float &radius)
  {
    /*
    Least Squares Circle Fitting method:
    Setup a linear system:
      (x - a)^2 + (y - b)^2 = r^2

    which can be rearranged to:
      2ax + 2by + (r^2 - a^2 - b^2) - x^2 - y^2 = 0
      2ax + 2by + (r^2 - a^2 - b^2) = x^2 + y^2
      2a*x + 2b*y + c = x^2 + y^2 
      (where c is the center of the circle r^2 - a^2 - b^2)

    We can express this as a matrix equation:
      A * X = B
    where:
      A = [x_i, y_i, 1], X = (2a, 2b, c), B = [x_i^2 + y_i^2] 

    We can then solve for X using least squares, which can be done using OpenCV's solve function.
    The solution will give us 2a, 2b, and c, from which we can derive the center (a, b) and radius (r = sqrt(c + a^2 + b^2)).
    */

    // Put the contour points into matrices
    Mat A(contour.size(), 3, CV_32F);
    Mat B(contour.size(), 1, CV_32F);
    Mat X(3, 1, CV_32F);

    // Get raw pointers to the data blocks outside the loop for better performance
    float* A_data = reinterpret_cast<float*>(A.data);
    float* B_data = reinterpret_cast<float*>(B.data);
    const size_t numPoints = contour.size();

    for (size_t i = 0; i < numPoints; ++i)
    {
      const auto& point = contour[i];

      A_data[3 * i + 0] = point.x;
      A_data[3 * i + 1] = point.y;
      A_data[3 * i + 2] = 1.0f;

      B_data[i] = point.x * point.x + point.y * point.y;
    }
    
    // Solve the linear system using SVD decomposition and compute the circle parameters
    solve( A, B, X, cv::DECOMP_SVD );
    center.x = X.at<float>(0) * 0.5f;
    center.y = X.at<float>(1) * 0.5f;
    radius = sqrt( X.at<float>(2) + center.x*center.x + center.y*center.y );
  }

  bool FindBall(const Mat& mask, Vec3f &ball)
  {
    constexpr double MIN_AREA = MIN_CONTOUR_AREA / 4.0; // Adjusted for downsampled image

    std::vector<std::vector<cv::Point>> contours;
    std::vector<cv::Vec4i> hierarchy;
    cv::findContours(mask, contours, hierarchy, cv::RETR_EXTERNAL, cv::CHAIN_APPROX_SIMPLE);

    // Find the most circular contour, that is of a minimum size
    double minError = MAX_CIRCLE_FIT_ERROR;
    int bestContourIndex = -1;
    for (int i = 0; i < contours.size(); ++i)
    {
      const std::vector<Point>& contour = contours[i];
      if (hierarchy[i][3] != -1) continue; // Skip internal contours
      if (contour.size() < MIN_AREA) continue; // Filter small contours

      Point2f center;
      float radius;
      FitCircleTaubin(contour, center, radius);
      double error = CircleFitError(contour, center, radius);
      if (error < minError)
      {
        minError = error;
        bestContourIndex = i;
        ball = Vec3f(center.x, center.y, radius);
      }
    }
    if (bestContourIndex == -1)
      return false; // No valid contour found
    
    return true;
  }

  bool TryDetectBall(const Mat& yuyvFrame, Vec3f& ball)
  {
    // Static variables to avoid reallocation
    static Mat v(CameraHelpers::IMAGE_HEIGHT / 2, CameraHelpers::IMAGE_WIDTH / 2, CV_8UC1);

    ExtractV(yuyvFrame, v);
    MaskV(v, v);
    bool ballFound = FindBall(v, ball);

    // Scale the ball coordinates to match the original image size
    ball *= 2.0f;

    return ballFound;
  }

  void CalculateTargetPosition(const Vec3f& ball, double &offsetX, double &offsetY)
  {
    constexpr unsigned int CENTER_X = CameraHelpers::IMAGE_WIDTH / 2;
    constexpr unsigned int CENTER_Y = CameraHelpers::IMAGE_HEIGHT / 2;
    constexpr double MOTOR_UNITS_PER_PIXEL = -CameraHelpers::RADIANS_PER_PIXEL / (2.0 * std::numbers::pi);

    // Calculate the offset from the center of the image
    int pixelOffsetX = static_cast<int>(ball[0]) - CENTER_X;
    int pixelOffsetY = static_cast<int>(ball[1]) - CENTER_Y;

    if (std::abs(pixelOffsetX) > PIXEL_THRESHOLD)
      offsetX = MOTOR_UNITS_PER_PIXEL * pixelOffsetX;
    else
      offsetX = 0.0;
    
    if (std::abs(pixelOffsetY) > PIXEL_THRESHOLD)
      offsetY = MOTOR_UNITS_PER_PIXEL * pixelOffsetY;
    else
      offsetY = 0.0;
  }
} // namespace ImageProcessing
