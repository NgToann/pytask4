

a = [1, 3, 5]
b = a
a[:] = [x + 99 for x in a]
print(b)

x = {'AB', 'BA', 'CD', 'AC'}
x.remove('AB')
print(x)