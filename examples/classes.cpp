#include <iostream>
#include <string>
#include <UnityEngine.h>
#include <IRRuntime.h>


namespace FCC
{
    class Meow
    {
        private:
            float speedX;
            float speedY;
            float speedZ;
        void OnStart()
        {
                speedX = 50;
                speedY = 90;
                speedZ = 50;
            // defaults are set via field initializers
        }

        void OnUpdate()
        {
            float dt = UnityEngine::Time::get_deltaTime();
            UnityEngine::GameObject cat = IRRuntime::GetSelf();
            UnityEngine::Vector3* Rotation = new UnityEngine::Vector3(speedX * dt, speedY * dt, speedZ * dt);
            cat.transform.Rotate(Rotation);
            UnityEngine::Debug::Log("Rotating cat by 50, 90, 50 degrees per second");
        }
    };

    
}
