#include <iostream>
#include "repro.h"
class Program {
public:
    
    int Main()
    {
        int v = get_value();
        std::cout << "get_value returned: " << v << "\n";
        return 0;
    }
};
