#ifndef UNITYENGINE_H
#define UNITYENGINE_H
namespace UnityEngine
{
    // -------------------------------------------------------------------------
    // Forward declarations
    // -------------------------------------------------------------------------

    class GameObject;
    class Transform;
    class Component;

    // -------------------------------------------------------------------------
    // Math primitives
    // -------------------------------------------------------------------------

    struct Vector2
    {
        public:
            float x;
            float y;

            Vector2(float x, float y);

            // Properties — call as get_magnitude() / get_normalized() etc.
            float   get_magnitude();        // HOT
            float   get_sqrMagnitude();
            Vector2 get_normalized();       // HOT

            // Static properties
            static Vector2 get_zero();
            static Vector2 get_one();
            static Vector2 get_up();
            static Vector2 get_down();
            static Vector2 get_left();
            static Vector2 get_right();

            // Static methods
            static float   Distance(Vector2 a, Vector2 b);
            static float   Dot(Vector2 a, Vector2 b);
            static Vector2 Lerp(Vector2 a, Vector2 b, float t);
            static Vector2 MoveTowards(Vector2 current, Vector2 target, float maxDelta);
            static Vector2 Reflect(Vector2 inDir, Vector2 normal);
            static float   Angle(Vector2 from, Vector2 to);

            // Operators (compile to arithmetic opcodes; for explicit dispatch use op_* below)
            Vector2 operator+(Vector2 b);
            Vector2 operator-(Vector2 b);
            Vector2 operator*(float scalar);
            Vector2 operator/(float scalar);
            bool    operator==(Vector2 b);
            bool    operator!=(Vector2 b);

            // Explicit operator methods (map to runtime op_* bindings)
            static Vector2 op_Addition(Vector2 a, Vector2 b);
            static Vector2 op_Subtraction(Vector2 a, Vector2 b);
            static Vector2 op_Multiply(Vector2 a, float scalar);
            static Vector2 op_Division(Vector2 a, float scalar);
            static bool    op_Equality(Vector2 a, Vector2 b);
            static bool    op_Inequality(Vector2 a, Vector2 b);
    };

    struct Vector3
    {
        public:
            float x;
            float y;
            float z;

            Vector3(float x, float y, float z);
            Vector3(float x, float y);

            // Properties
            float   get_magnitude();        // HOT
            float   get_sqrMagnitude();
            Vector3 get_normalized();       // HOT

            // Static properties
            static Vector3 get_zero();
            static Vector3 get_one();
            static Vector3 get_up();
            static Vector3 get_down();
            static Vector3 get_left();
            static Vector3 get_right();
            static Vector3 get_forward();
            static Vector3 get_back();
            static Vector3 get_positiveInfinity();
            static Vector3 get_negativeInfinity();

            // Static methods
            static float   Distance(Vector3 a, Vector3 b);          // HOT
            static float   Dot(Vector3 a, Vector3 b);
            static Vector3 Cross(Vector3 a, Vector3 b);
            static Vector3 Lerp(Vector3 a, Vector3 b, float t);     // HOT
            static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t);
            static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDelta);
            static Vector3 Normalize(Vector3 v);
            static Vector3 Scale(Vector3 a, Vector3 b);
            static Vector3 Reflect(Vector3 inDir, Vector3 normal);
            static float   Angle(Vector3 from, Vector3 to);
            static float   SignedAngle(Vector3 from, Vector3 to, Vector3 axis);
            static Vector3 ClampMagnitude(Vector3 v, float maxLength);
            static Vector3 Project(Vector3 v, Vector3 onNormal);
            static Vector3 ProjectOnPlane(Vector3 v, Vector3 planeNormal);
            static Vector3 RotateTowards(Vector3 current, Vector3 target, float maxRadDelta, float maxMagDelta);

            // Operators
            Vector3 operator+(Vector3 b);   // HOT
            Vector3 operator-(Vector3 b);   // HOT
            Vector3 operator*(float scalar);// HOT
            Vector3 operator/(float scalar);
            Vector3 operator-();
            bool    operator==(Vector3 b);
            bool    operator!=(Vector3 b);

            // Explicit operator methods
            static Vector3 op_Addition(Vector3 a, Vector3 b);
            static Vector3 op_Subtraction(Vector3 a, Vector3 b);
            static Vector3 op_Multiply(Vector3 a, float scalar);
            static Vector3 op_Multiply(float scalar, Vector3 a);
            static Vector3 op_Division(Vector3 a, float scalar);
            static Vector3 op_UnaryNegation(Vector3 a);
            static bool    op_Equality(Vector3 a, Vector3 b);
            static bool    op_Inequality(Vector3 a, Vector3 b);
    };

    struct Quaternion
    {
        public:
            float x;
            float y;
            float z;
            float w;

            Quaternion(float x, float y, float z, float w);

            // Properties
            Vector3 get_eulerAngles();

            // Static properties
            static Quaternion get_identity();

            // Static methods
            static Quaternion Euler(float x, float y, float z);
            static Quaternion Euler(Vector3 euler);
            static Quaternion AngleAxis(float angle, Vector3 axis);
            static Quaternion LookRotation(Vector3 forward);
            static Quaternion LookRotation(Vector3 forward, Vector3 up);
            static Quaternion Slerp(Quaternion a, Quaternion b, float t);
            static Quaternion Lerp(Quaternion a, Quaternion b, float t);
            static Quaternion LerpUnclamped(Quaternion a, Quaternion b, float t);
            static Quaternion RotateTowards(Quaternion from, Quaternion to, float maxDeg);
            static float      Angle(Quaternion a, Quaternion b);
            static Quaternion Inverse(Quaternion q);
            static Quaternion FromToRotation(Vector3 from, Vector3 to);

            // Explicit operator methods
            static Quaternion op_Multiply(Quaternion a, Quaternion b);
            static Vector3    op_Multiply(Quaternion q, Vector3 v);
    };

    struct Color
    {
        public:
            float r;
            float g;
            float b;
            float a;

            Color(float r, float g, float b, float a);
            Color(float r, float g, float b);

            // Static properties
            static Color get_red();
            static Color get_green();
            static Color get_blue();
            static Color get_white();
            static Color get_black();
            static Color get_yellow();
            static Color get_cyan();
            static Color get_magenta();
            static Color get_clear();
            static Color get_gray();
            static Color get_grey();

            // Static methods
            static Color Lerp(Color a, Color b, float t);
            static Color LerpUnclamped(Color a, Color b, float t);
    };

    struct Ray
    {
        public:
            Vector3 origin;
            Vector3 direction;

            Ray(Vector3 origin, Vector3 direction);
    };

    struct Touch
    {
        public:
            int     fingerId;
            Vector2 position;
            Vector2 deltaPosition;
            float   deltaTime;
            int     phase;      // TouchPhase enum value
            int     tapCount;
    };

    // -------------------------------------------------------------------------
    // Math & time utilities
    // -------------------------------------------------------------------------

    class Mathf
    {
        public:
            // Constants — call as Mathf::get_PI() etc.
            static float get_PI();
            static float get_Infinity();
            static float get_NegativeInfinity();
            static float get_Epsilon();
            static float get_Deg2Rad();
            static float get_Rad2Deg();

            // Trig
            static float Sin(float angle);          // HOT
            static float Cos(float angle);          // HOT
            static float Tan(float angle);
            static float Asin(float value);
            static float Acos(float value);
            static float Atan(float value);
            static float Atan2(float y, float x);

            // Exponential / logarithmic
            static float Sqrt(float value);         // HOT
            static float Pow(float base_, float exponent);
            static float Log(float value);
            static float Log(float value, float base_);
            static float Log10(float value);
            static float Exp(float value);

            // Rounding
            static float Floor(float value);
            static int   FloorToInt(float value);
            static float Ceil(float value);
            static int   CeilToInt(float value);
            static float Round(float value);
            static int   RoundToInt(float value);
            static float Abs(float value);          // HOT
            static int   Abs(int value);

            // Range / clamp
            static float Clamp(float value, float min, float max);  // HOT
            static int   Clamp(int value, int min, int max);
            static float Clamp01(float value);
            static float Min(float a, float b);
            static int   Min(int a, int b);
            static float Max(float a, float b);
            static int   Max(int a, int b);
            static float Sign(float value);

            // Interpolation
            static float Lerp(float a, float b, float t);           // HOT
            static float LerpUnclamped(float a, float b, float t);
            static float LerpAngle(float a, float b, float t);
            static float InverseLerp(float a, float b, float value);
            static float SmoothStep(float from, float to, float t);
            static float SmoothDamp(float current, float target, float currentVelocity, float smoothTime);

            // Motion / angle
            static float MoveTowards(float current, float target, float maxDelta);     // HOT
            static float MoveTowardsAngle(float current, float target, float maxDelta);
            static float DeltaAngle(float current, float target);
            static float PingPong(float t, float length);
            static float Repeat(float t, float length);

            // Integer utilities
            static bool IsPowerOfTwo(int value);
            static int  NextPowerOfTwo(int value);

            // Approximation
            static bool Approximately(float a, float b);
    };

    class Time
    {
        public:
            // All time values are properties — call as Time::get_deltaTime() etc.
            static float get_deltaTime();           // HOT — use in OnUpdate
            static float get_fixedDeltaTime();      // HOT — use in OnFixedUpdate
            static void  set_fixedDeltaTime(float value);
            static float get_time();
            static float get_unscaledTime();
            static float get_unscaledDeltaTime();
            static float get_timeScale();
            static void  set_timeScale(float value);
            static int   get_frameCount();
            static float get_realtimeSinceStartup();
            static float get_smoothDeltaTime();
            static float get_timeSinceLevelLoad();
            static int   get_renderedFrameCount();
    };

    class Random
    {
        public:
            static float      get_value();              // [0.0, 1.0]
            static Vector2    get_insideUnitCircle();
            static Vector3    get_insideUnitSphere();
            static Vector3    get_onUnitSphere();
            static Quaternion get_rotation();
            static Quaternion get_rotationUniform();

            static float Range(float min, float max);
            static int   Range(int min, int max);
    };

    // -------------------------------------------------------------------------
    // Input
    // -------------------------------------------------------------------------

    class Input
    {
        public:
            // Keyboard
            static bool GetKey(std::string keyName);            // HOT
            static bool GetKeyDown(std::string keyName);        // HOT
            static bool GetKeyUp(std::string keyName);

            // Virtual buttons
            static bool GetButton(std::string buttonName);      // HOT
            static bool GetButtonDown(std::string buttonName);  // HOT
            static bool GetButtonUp(std::string buttonName);

            // Axes
            static float GetAxis(std::string axisName);         // HOT
            static float GetAxisRaw(std::string axisName);

            // Mouse
            static bool    GetMouseButton(int button);
            static bool    GetMouseButtonDown(int button);
            static bool    GetMouseButtonUp(int button);
            static Vector3 get_mousePosition();                 // HOT
            static Vector2 get_mouseScrollDelta();

            // Touch
            static int   get_touchCount();
            static Touch GetTouch(int index);

            // Misc
            static bool        get_anyKey();
            static bool        get_anyKeyDown();
            static std::string get_inputString();
            static int         get_deviceOrientation();
            static Vector3     get_acceleration();
    };

    // -------------------------------------------------------------------------
    // Core object hierarchy
    // -------------------------------------------------------------------------

    class Object
    {
        public:
            // 'name' is accessible as a field (ldfld) or via get_name() / set_name() (hot path)
            std::string name;
            std::string get_name();
            void        set_name(std::string value);

            std::string ToString();

            static Object Instantiate(Object obj);
            static Object Instantiate(Object obj, Vector3 position, Quaternion rotation);
            static Object Instantiate(Object obj, Transform parent);
            static void   Destroy(Object obj);
            static void   Destroy(Object obj, float delay);
            static void   DestroyImmediate(Object obj);
            static void   DontDestroyOnLoad(Object obj);
            static Object FindObjectOfType(std::string typeName);
    };

    class Component : public Object
    {
        public:
            // Properties — use get_* methods for the hot-path native binding
            std::string get_tag();
            void        set_tag(std::string value);
            GameObject  get_gameObject();   // HOT
            Transform   get_transform();    // HOT

            bool CompareTag(std::string tag);
    };

    class Transform : public Component
    {
        public:
            // Position
            Vector3    get_position();                      // HOT
            void       set_position(Vector3 value);         // HOT
            Vector3    get_localPosition();
            void       set_localPosition(Vector3 value);

            // Rotation
            Quaternion get_rotation();
            void       set_rotation(Quaternion value);
            Quaternion get_localRotation();
            void       set_localRotation(Quaternion value);
            Vector3    get_eulerAngles();
            void       set_eulerAngles(Vector3 value);
            Vector3    get_localEulerAngles();
            void       set_localEulerAngles(Vector3 value);

            // Scale
            Vector3    get_localScale();
            void       set_localScale(Vector3 value);
            Vector3    get_lossyScale();                    // read-only

            // Direction vectors (read-only)
            Vector3    get_forward();
            Vector3    get_right();
            Vector3    get_up();

            // Hierarchy
            Transform  get_parent();
            void       set_parent(Transform value);
            int        get_childCount();

            // Movement
            void Translate(Vector3 translation);            // HOT
            void Translate(float x, float y, float z);     // HOT
            void Rotate(Vector3 eulerAngles);               // HOT
            void Rotate(float x, float y, float z);        // HOT
            void RotateAround(Vector3 point, Vector3 axis, float angle);
            void LookAt(Vector3 worldPosition);
            void LookAt(Transform target);
            void SetPositionAndRotation(Vector3 position, Quaternion rotation);

            // Hierarchy management
            Transform GetChild(int index);
            int       GetChildCount();
            Transform Find(std::string name);
            bool      IsChildOf(Transform parent);
            void      SetParent(Transform parent);
            void      SetParent(Transform parent, bool worldPositionStays);
            void      SetAsFirstSibling();
            void      SetAsLastSibling();
            void      DetachChildren();

            // Space conversion
            Vector3 TransformPoint(Vector3 position);
            Vector3 InverseTransformPoint(Vector3 position);
            Vector3 TransformDirection(Vector3 direction);
            Vector3 InverseTransformDirection(Vector3 direction);
    };

    class GameObject : public Object
    {
        public:
            // Properties
            bool        get_activeSelf();
            bool        get_activeInHierarchy();
            bool        get_isStatic();
            std::string get_tag();
            void        set_tag(std::string value);
            int         get_layer();
            void        set_layer(int value);
            Transform   get_transform();    // HOT

            // Constructors
            GameObject();
            GameObject(std::string name);

            // Static factory
            static GameObject Find(std::string name);
            static GameObject FindWithTag(std::string tag);
            static GameObject FindGameObjectWithTag(std::string tag);
            static GameObject CreatePrimitive(int type);

            // Instance methods
            void      SetActive(bool value);
            bool      CompareTag(std::string tag);
            Component GetComponent(std::string typeName);
            Component AddComponent(std::string typeName);
            bool      TryGetComponent(std::string typeName, Component result);
            Component GetComponentInChildren(std::string typeName);
            Component GetComponentInParent(std::string typeName);
            void      SendMessage(std::string methodName);
            void      SendMessage(std::string methodName, Object value);
            void      BroadcastMessage(std::string methodName);
    };

    // -------------------------------------------------------------------------
    // Scripting base
    // -------------------------------------------------------------------------

    class Behaviour : public Component
    {
        public:
            bool get_enabled();
            void set_enabled(bool value);
            bool get_isActiveAndEnabled();
    };

    class MonoBehaviour : public Behaviour
    {
        public:
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
            static bool get_isDebugBuild();

            static void Log(std::string message);                           // HOT
            static void Log(Object message);                                // HOT
            static void Log(std::string message, Object context);
            static void LogWarning(std::string message);
            static void LogWarning(Object message);
            static void LogWarning(std::string message, Object context);
            static void LogError(std::string message);
            static void LogError(Object message);
            static void LogError(std::string message, Object context);
            static void LogException(std::string exception);
            static void Assert(bool condition);
            static void Assert(bool condition, std::string message);
            static void DrawLine(Vector3 start, Vector3 end);
            static void DrawLine(Vector3 start, Vector3 end, Color color);
            static void DrawRay(Vector3 start, Vector3 direction);
            static void DrawRay(Vector3 start, Vector3 direction, Color color);
            static void Break();
            static void ClearDeveloperConsole();
    };

    // -------------------------------------------------------------------------
    // Application & platform
    // -------------------------------------------------------------------------

    class Application
    {
        public:
            static bool        get_isPlaying();
            static bool        get_isEditor();
            static bool        get_isFocused();
            static int         get_platform();
            static std::string get_dataPath();
            static std::string get_persistentDataPath();
            static std::string get_temporaryCachePath();
            static std::string get_streamingAssetsPath();
            static std::string get_version();
            static std::string get_productName();
            static std::string get_companyName();
            static std::string get_unityVersion();
            static int         get_targetFrameRate();
            static void        set_targetFrameRate(int value);
            static bool        get_runInBackground();
            static void        set_runInBackground(bool value);

            static void Quit();
            static void Quit(int exitCode);
            static void OpenURL(std::string url);
    };

    class Screen
    {
        public:
            static int   get_width();
            static int   get_height();
            static float get_dpi();
            static bool  get_fullScreen();
            static void  set_fullScreen(bool value);
            static void  SetResolution(int width, int height, bool fullScreen);
    };

    class Cursor
    {
        public:
            static bool get_visible();
            static void set_visible(bool value);
            static int  get_lockState();
            static void set_lockState(int value);
    };

    // -------------------------------------------------------------------------
    // Rendering
    // -------------------------------------------------------------------------

    class Camera : public Component
    {
        public:
            static Camera get_main();       // HOT
            static Camera get_current();

            float get_fieldOfView();
            void  set_fieldOfView(float value);
            float get_nearClipPlane();
            float get_farClipPlane();
            bool  get_orthographic();
            void  set_orthographic(bool value);
            Color get_backgroundColor();
            void  set_backgroundColor(Color value);

            Vector3 ScreenToWorldPoint(Vector3 position);
            Vector3 WorldToScreenPoint(Vector3 position);
            Ray     ScreenPointToRay(Vector3 position);
    };

    // -------------------------------------------------------------------------
    // Physics
    // -------------------------------------------------------------------------

    class Physics
    {
        public:
            static Vector3 get_gravity();
            static void    set_gravity(Vector3 value);
            static float   get_bounceThreshold();

            static bool   Raycast(Vector3 origin, Vector3 direction);
            static bool   Raycast(Vector3 origin, Vector3 direction, float maxDistance);
            // Returns array of Collider objects at runtime
            static Object OverlapSphere(Vector3 position, float radius);
    };

    class Physics2D
    {
        public:
            static Vector2 get_gravity();
            static void    set_gravity(Vector2 value);

            static bool   Raycast(Vector2 origin, Vector2 direction);
            static bool   Raycast(Vector2 origin, Vector2 direction, float distance);
            // Returns array of Collider2D objects at runtime
            static Object OverlapCircle(Vector2 point, float radius);
    };

    // -------------------------------------------------------------------------
    // Scene Management
    // -------------------------------------------------------------------------

    namespace SceneManagement
    {
        class SceneManager
        {
            public:
                static int get_sceneCount();

                static void        LoadScene(std::string sceneName);
                static void        LoadScene(int buildIndex);
                static void        LoadScene(std::string sceneName, int mode);
                static void        UnloadSceneAsync(std::string sceneName);
                static std::string GetActiveSceneName();
                static std::string GetActiveScene();
                static std::string GetSceneByName(std::string name);
        };
    }
}

#endif // UNITYENGINE_H