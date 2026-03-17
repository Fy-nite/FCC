#include <iostream>
namespace test {
    class Program {
        public: 
            static int get_value() {
                return 42;
            }
            static void Main() {
                std::cout << "Hello, World!" << std::endl;
                int v = get_value();
                std::cout << "get_value returned: " << v << "\n";
            }
    };
}