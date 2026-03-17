#include <UnityEngine.h>
class IRRuntime  {
    public: 
        static UnityEngine::GameObject GetOwner();
        static UnityEngine::GameObject GetSelf();
};