#include <iostream>
#include "repro.h"

int main()
{
    int v = get_value();
    std::cout << "get_value returned: " << v << "\n";
    return 0;
}
