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

        void OnStart()
        {
                speedX = 50;
                speedY = 90;
                speedZ = 50;
            // defaults are set via field initializers
        }

        void OnUpdate()
        {
            // UnityEngine::Debug::Log("Meow is updating...");
            // UnityEngine::Debug::Log(speedX);
            // UnityEngine::Debug::Log(speedY);
            // UnityEngine::Debug::Log(speedZ);
            float dt = UnityEngine::Time::get_deltaTime();
            UnityEngine::GameObject cat = IRRuntime::GetSelf();
            float rotX = speedX * dt;
            float rotY = speedY * dt;
            float rotZ = speedZ * dt;
            UnityEngine::Transform tf = cat.get_transform();
            tf.Rotate(rotX, rotY, rotZ);


            // UnityEngine::Debug::Log("Rotating cat by 50, 90, 50 degrees per second");
        }
    };

    
}
