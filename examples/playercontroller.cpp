#include "UnityEngine.h"
#include "IRRuntime.h"
namespace FCC
{
    class PlayerController
    {
        UnityEngine::Transform transform;
        UnityEngine::Camera cam;
        UnityEngine::GameObject player;
        void OnStart()
        {
            UnityEngine::Debug::Log("PlayerController started!");
            player = IRRuntime::GetSelf();
            transform = player.get_transform();
            cam = UnityEngine::Camera::get_main();
        }

        void OnUpdate()
        {
            UnityEngine::Debug::Log("PlayerController updating...");
            UnityEngine::Debug::Log(transform.get_position());
            UnityEngine::Debug::Log(cam.get_fieldOfView());
        }
    };
}