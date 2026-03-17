/* C implementation that (incorrectly) returns void instead of int */
#include <stdio.h>

void get_value()
{
    /* no return value */
    puts("impl.c: get_value called (void)");
}
