using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Javax.Microedition.Khronos.Opengles;
using Com.Pikkart.AR.Recognition;
using Javax.Microedition.Khronos.Egl;
using System.Threading.Tasks;

namespace PikkartSample.Droid
{
    public class ARRenderer : GLTextureView.Renderer
    {
        public bool IsActive = false;
        //the rendering viewport dimensions
        private int ViewportWidth;
        private int ViewportHeight;
        //normalized screen orientation (0=landscale, 90=portrait, 180=inverse landscale, 270=inverse portrait)
        private int Angle;
        //
        private Context context;
        //the 3d object we will render on the marker
        private Mesh monkeyMesh = null;
        private Mesh monkeyMeshYellow = null;
        private Mesh monkeyMeshBlue = null;
        private Mesh monkeyMeshRed = null;
        private Mesh monkeyMeshGray = null;

        ProgressDialog progressDialog;

        /* Constructor. */
        public ARRenderer(Context con)
        {
            context = con;
            progressDialog = new ProgressDialog(con);
        }

        /** Called when the surface is created or recreated. 
          * Reinitialize OpenGL related stuff here*/
        public void OnSurfaceCreated(IGL10 gl, EGLConfig config)
        {
            gl.GlClearColor(1.0f, 1.0f, 1.0f, 1.0f);
            //Here we create the 3D object and initialize textures, shaders, etc.

            monkeyMesh = new Mesh();
            monkeyMeshYellow = new Mesh();
            monkeyMeshBlue = new Mesh();
            monkeyMeshRed = new Mesh();
            monkeyMeshGray = new Mesh();

            Task.Run(async() =>
            {
                try
                {
                    ((Activity)context).RunOnUiThread(() =>
                    {
                        progressDialog = ProgressDialog.Show(context, "Loading textures", "The 3D template textures of this tutorial have not been loaded yet\n(this may take a while)", true);
                    });
                    monkeyMesh.InitMesh(context.Assets, "media/monkey.json", "media/texture.png");
                    monkeyMeshYellow.InitMesh(context.Assets, "media/monkey.json", "media/texture.png");
                    monkeyMeshBlue.InitMesh(context.Assets, "media/monkey.json", "media/texture2.png");
                    monkeyMeshRed.InitMesh(context.Assets, "media/monkey.json", "media/texture3.png");
                    monkeyMeshGray.InitMesh(context.Assets, "media/monkey.json", "media/texturegray.png");
                    monkeyMesh = monkeyMeshGray; // Initially we want to display the gray monkey

                    if (progressDialog != null)
                        progressDialog.Dismiss();
                    
                }
                catch (System.OperationCanceledException ex)
                {
                    Console.WriteLine("init failed: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            });
        }

        /** Called when the surface changed size. */
        public void onSurfaceChanged(IGL10 gl, int width, int height)
        {
        }

        /** Called when the surface is destroyed. */
        public void onSurfaceDestroyed()
        {
        }

        /** Here we compute the model-view-projection matrix for OpenGL rendering
          * from the model-view and projection matrix computed by Pikkart's AR SDK.
          * the projection matrix is rotated accordingly to the screen orientation */
        public bool computeModelViewProjectionMatrix(float[] mvpMatrix)
        {
            RenderUtils.matrix44Identity(mvpMatrix);

            float w = (float)640;
            float h = (float)480;

            float ar = (float)ViewportHeight / (float)ViewportWidth;
            if (ViewportHeight > ViewportWidth) ar = 1.0f / ar;
            float h1 = h, w1 = w;
            if (ar < h / w)
                h1 = w * ar;
            else
                w1 = h / ar;

            float a = 0f, b = 0f;
            switch (Angle)
            {
                case 0:
                    a = 1f; b = 0f;
                    break;
                case 90:
                    a = 0f; b = 1f;
                    break;
                case 180:
                    a = -1f; b = 0f;
                    break;
                case 270:
                    a = 0f; b = -1f;
                    break;
                default: break;
            }

            float[] angleMatrix = new float[16];

            angleMatrix[0] = a; angleMatrix[1] = b; angleMatrix[2] = 0.0f; angleMatrix[3] = 0.0f;
            angleMatrix[4] = -b; angleMatrix[5] = a; angleMatrix[6] = 0.0f; angleMatrix[7] = 0.0f;
            angleMatrix[8] = 0.0f; angleMatrix[9] = 0.0f; angleMatrix[10] = 1.0f; angleMatrix[11] = 0.0f;
            angleMatrix[12] = 0.0f; angleMatrix[13] = 0.0f; angleMatrix[14] = 0.0f; angleMatrix[15] = 1.0f;

            float[] projectionMatrix =(float[]) RecognitionFragment.GetCurrentProjectionMatrix().Clone();
            projectionMatrix[5] = projectionMatrix[5] * (h / h1);

            float[] correctedProjection = new float[16];

            RenderUtils.matrixMultiply(4, 4, angleMatrix, 4, 4, projectionMatrix, correctedProjection);

            if (RecognitionFragment.IsTracking)
            {
                float[] modelviewMatrix = RecognitionFragment.GetCurrentModelViewMatrix();
                float[] temp_mvp = new float[16];
                RenderUtils.matrixMultiply(4, 4, correctedProjection, 4, 4, modelviewMatrix, temp_mvp);
                RenderUtils.matrix44Transpose(temp_mvp, mvpMatrix);
                return true;
            }
            return false;
        }

        /** Called to draw the current frame. */
        public void onDrawFrame(IGL10 gl)
        {
            if (!IsActive) return;

            gl.GlClear(GL10.GlColorBufferBit | GL10.GlDepthBufferBit);

            // Call our native function to render camera content
            RecognitionFragment.RenderCamera(ViewportWidth, ViewportHeight, Angle);

            float[] mvpMatrix = new float[16];
            if (computeModelViewProjectionMatrix(mvpMatrix))
            {
                //draw our 3d mesh on top of the marker
                if (monkeyMesh != null && monkeyMesh.MeshLoaded)
                {
                    if (monkeyMesh.GLLoaded)
                        monkeyMesh.DrawMesh(mvpMatrix);
                    else
                        monkeyMesh.InitMeshGL();

                    RenderUtils.CheckGLError("completed Monkey head Render");
                }
            }

            gl.GlFinish();
        }

        /* this will be called by our GLTextureView-derived class to update screen sizes and orientation */
        public void UpdateViewport(int viewportWidth, int viewportHeight, int angle)
        {
            ViewportWidth = viewportWidth;
            ViewportHeight = viewportHeight;
            Angle = angle;
        }

        public void SelectMonkey(int monkeyID)
        {
            switch (monkeyID)
            {
                case 0:
                    monkeyMesh = monkeyMeshGray;
                    break;
                case 1:
                    monkeyMesh = monkeyMeshYellow;
                    break;
                case 2:
                    monkeyMesh = monkeyMeshBlue;
                    break;
                case 3:
                    monkeyMesh = monkeyMeshRed;
                    break;
                default:
                    monkeyMesh = monkeyMeshGray;
                    break;
            }
        }
    }
}