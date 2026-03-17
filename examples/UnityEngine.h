

namespace UnityEngine
{
    // -------------------------------------------------------------------------
    // Math primitives
    // -------------------------------------------------------------------------

    struct Vector2
    {
        public:
            float x;
            float y;

            Vector2(float x, float y);

            static Vector2 zero();
            static Vector2 one();
            static Vector2 up();
            static Vector2 down();
            static Vector2 left();
            static Vector2 right();

            float magnitude();
            Vector2 normalized();
            static float Distance(Vector2 a, Vector2 b);
            static float Dot(Vector2 a, Vector2 b);
            static Vector2 Lerp(Vector2 a, Vector2 b, float t);
    };

    struct Vector3
    {
        public:
            float x;
            float y;
            float z;

            Vector3(float x, float y, float z);

            static Vector3 zero();
            static Vector3 one();
            static Vector3 up();
            static Vector3 down();
            static Vector3 left();
            static Vector3 right();
            static Vector3 forward();
            static Vector3 back();

            float magnitude();
            Vector3 normalized();
            static float Distance(Vector3 a, Vector3 b);
            static float Dot(Vector3 a, Vector3 b);
            static Vector3 Cross(Vector3 a, Vector3 b);
            static Vector3 Lerp(Vector3 a, Vector3 b, float t);
    };

    struct Quaternion
    {
        public:
            float x;
            float y;
            float z;
            float w;

            Quaternion(float x, float y, float z, float w);

            static Quaternion identity();
            static Quaternion Euler(float x, float y, float z);
            static Quaternion LookRotation(Vector3 forward, Vector3 up);
            static Quaternion Slerp(Quaternion a, Quaternion b, float t);
            Vector3 eulerAngles();
    };

    struct Color
    {
        public:
            float r;
            float g;
            float b;
            float a;

            Color(float r, float g, float b, float a);

            static Color red();
            static Color green();
            static Color blue();
            static Color white();
            static Color black();
            static Color yellow();
            static Color cyan();
            static Color magenta();
            static Color clear();
            static Color Lerp(Color a, Color b, float t);
    };

    // -------------------------------------------------------------------------
    // Math & time utilities
    // -------------------------------------------------------------------------

    class Mathf
    {
        public:
            static float PI;
            static float Infinity;
            static float NegativeInfinity;
            static float Epsilon;

            static float Abs(float value);
            static float Ceil(float value);
            static float Floor(float value);
            static float Round(float value);
            static float Sqrt(float value);
            static float Pow(float base, float exponent);
            static float Log(float value);
            static float Sin(float angle);
            static float Cos(float angle);
            static float Tan(float angle);
            static float Min(float a, float b);
            static float Max(float a, float b);
            static float Clamp(float value, float min, float max);
            static float Clamp01(float value);
            static float Lerp(float a, float b, float t);
            static float LerpUnclamped(float a, float b, float t);
            static float MoveTowards(float current, float target, float maxDelta);
            static float DeltaAngle(float current, float target);
            static bool Approximately(float a, float b);
    };

    class Time
    {
        public:
            static float deltaTime;
            static float fixedDeltaTime;
            static float time;
            static float unscaledTime;
            static float unscaledDeltaTime;
            static float timeScale;
            static int frameCount;
            static float realtimeSinceStartup;
            static float get_deltaTime();
    };

    // -------------------------------------------------------------------------
    // Input
    // -------------------------------------------------------------------------

    class Input
    {
        public:
            static bool GetKey(std::string keyName);
            static bool GetKeyDown(std::string keyName);
            static bool GetKeyUp(std::string keyName);
            static bool GetMouseButton(int button);
            static bool GetMouseButtonDown(int button);
            static bool GetMouseButtonUp(int button);
            static Vector3 mousePosition();
            static float GetAxis(std::string axisName);
            static float GetAxisRaw(std::string axisName);
    };

    // -------------------------------------------------------------------------
    // Core object hierarchy
    // -------------------------------------------------------------------------

    class Object
    {
        public:
            std::string name;

            static void Destroy(Object obj);
            static void Destroy(Object obj, float delay);
            static void DontDestroyOnLoad(Object obj);
            std::string ToString();
    };

    // Forward declarations to allow members referencing types defined later
    class GameObject;
    class Transform;

    class Component : public Object
    {
        public:
            GameObject gameObject;
            Transform transform;
            std::string tag;

            bool CompareTag(std::string tag);
    };

    class Transform : public Component
    {
        public:
            Vector3 position;
            Vector3 localPosition;
            Quaternion rotation;
            Quaternion localRotation;
            Vector3 localScale;
            Vector3 eulerAngles;
            Vector3 localEulerAngles;
            Vector3 forward;
            Vector3 right;
            Vector3 up;
            Transform parent;
            int childCount;

            void Translate(Vector3 translation);
            void Translate(float x, float y, float z);
            void Rotate(Vector3 eulerAngles);
            void Rotate(Quaternion rotation);
            void Rotate(Vector3* axis);
            void RotateAround(Vector3 point, Vector3 axis, float angle);
            void LookAt(Transform target);
            void LookAt(Vector3 worldPosition);
            Transform GetChild(int index);
            void SetParent(Transform parent);
            void SetParent(Transform parent, bool worldPositionStays);
            Vector3 TransformPoint(Vector3 position);
            Vector3 InverseTransformPoint(Vector3 position);
            Vector3 TransformDirection(Vector3 direction);
    };

    class GameObject : public Object
    {
        public:
            bool activeSelf;
            bool activeInHierarchy;
            bool isStatic;
            std::string tag;
            Transform transform;

            GameObject();
            GameObject(std::string name);

            void SetActive(bool value);
            bool CompareTag(std::string tag);

            static GameObject Find(std::string name);
            static GameObject FindWithTag(std::string tag);

            // Note: GetComponent/AddComponent are templated in real Unity,
            // represented here with a type-name string for header completeness.
            Component GetComponent(std::string typeName);
            Component AddComponent(std::string typeName);
            bool TryGetComponent(std::string typeName, Component result);
            Component GetComponentInChildren(std::string typeName);
            Component GetComponentInParent(std::string typeName);
            Transform get_transform();

            void SendMessage(std::string methodName);
            void SendMessage(std::string methodName, std::string value);
            void BroadcastMessage(std::string methodName);
    };

    // -------------------------------------------------------------------------
    // Scripting base
    // -------------------------------------------------------------------------

    class MonoBehaviour : public Component
    {
        public:
            bool enabled;
            bool isActiveAndEnabled;

            void Invoke(std::string methodName, float time);
            void InvokeRepeating(std::string methodName, float time, float repeatRate);
            void CancelInvoke(std::string methodName);
            bool IsInvoking(std::string methodName);

            void StartCoroutine(std::string methodName);
            void StopCoroutine(std::string methodName);
            void StopAllCoroutines();
    };

    // -------------------------------------------------------------------------
    // Debug & diagnostics
    // -------------------------------------------------------------------------

    class Debug
    {
        public:
            static void Log(std::string message);
            static void LogWarning(std::string message);
            static void LogError(std::string message);
            static void LogException(std::string exception);
            static void DrawLine(Vector3 start, Vector3 end);
            static void DrawLine(Vector3 start, Vector3 end, Color color);
            static void DrawRay(Vector3 start, Vector3 direction);
            static void DrawRay(Vector3 start, Vector3 direction, Color color);
            static void Break();
            static void ClearDeveloperConsole();
    };
}
