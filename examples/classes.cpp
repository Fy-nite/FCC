#include <iostream>
#include <string>
#include <UnityEngine.h>
#include <IRRuntime.h>

namespace FCC
{
    class Meow
    {
        static float speedX;
        static float speedY;
        static float speedZ;
        static float RotSpeed;
        void OnStart()
        {
            // defaults are set via field initializers
            RotSpeed = 50.0f;
        }

        void OnUpdate()
        {
            float dt = UnityEngine::Time::get_deltaTime();
            float accel = RotSpeed * 50; // change as needed
            float damping = 5.0f;   // higher = quicker stop

            if (UnityEngine::Input::GetKey("up"))
                speedX -= accel * dt;
            else if (UnityEngine::Input::GetKey("down"))
                speedX += accel * dt;

            // apply damping when no input
            if (!UnityEngine::Input::GetKey("up") && !UnityEngine::Input::GetKey("down"))
                speedX = speedX * (1.0f / (1.0f + damping * dt)); // simple decay

            // // clamp small values to zero
            // if (fabs(speedX) < 0.01f)
            //     speedX = 0.0f;

            // same for speedY with left/right
            if (UnityEngine::Input::GetKey("right"))
                speedY -= accel * dt;
            else if (UnityEngine::Input::GetKey("left"))
                speedY += accel * dt;

            // apply damping when no input
            if (!UnityEngine::Input::GetKey("right") && !UnityEngine::Input::GetKey("left"))
                speedY = speedY * (1.0f / (1.0f + damping * dt)); // simple decay

            // // clamp small values to 
            // if (fabs(speedY) < 0.01f) speedY = 0.0f;

            float rotX = speedX * dt;
            float rotY = speedY * dt;
            float rotZ = speedZ * dt;
            UnityEngine::Transform tf = IRRuntime::GetSelf().get_transform();
            tf.Translate(rotX, rotY, rotZ);

            speedX = 0;
            speedY = 0;
            speedZ = 0;

            // UnityEngine::Debug::Log("Rotating cat by 50, 90, 50 degrees per second");
        }
    };

}
