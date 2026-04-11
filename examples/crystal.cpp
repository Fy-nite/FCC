// example_crystal_block.cpp
// Standalone C++ version of the ObjectIR CrystalBlock class.
// No Minecraft references — suitable for feeding into a C++->ObjectIR tool.
#include <iostream>
#include <string>

class CrystalBlock {
public:
std::string getIconName() const {
return "wool";
}

std::string getName() const {    return "Crystal Block";}
float getHardness() const {    return 2.5f;}
bool use(int x, int y, int z) {    std::cout << "[CrystalBlock] Activated at "              << x.ToString() << ", " << y.ToString() << ", " << z.ToString() << std::endl;    return true;}
void onPlace(int x, int y, int z) {    std::cout << "[CrystalBlock] Placed at "              << x.ToString() << ", " << y.ToString() << ", " << z.ToString() << std::endl;}
void onRemove(int x, int y, int z) {    std::cout << "[CrystalBlock] Removed at "              << x.ToString() << ", " << y.ToString() << ", " << z.ToString() << std::endl;}
};

#ifdef EXAMPLE_CRYSTAL_BLOCK_MAIN
int main() {
CrystalBlock b;
std::cout << "Icon: " << b.getIconName() << "\n";
std::cout << "Name: " << b.getName() << "\n";
std::cout << "Hardness: " << b.getHardness() << "\n";
b.onPlace(10, 64, 10);
b.use(10, 64, 10);
b.onRemove(10, 64, 10);
return 0;
}
#endif