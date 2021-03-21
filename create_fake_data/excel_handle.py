# Reading an excel file using Python
import xlrd
import json

# create a formatted string of the Python JSON object
def jprint(obj):
    text = json.dumps(obj, sort_keys=False, indent=4)
    print(text)

# Give the location of the file
loc = ('excel_input/' + 'bben.xlsx')
# To open Workbook
wb = xlrd.open_workbook(loc)
# sheet 0
sheet = wb.sheet_by_index(0)
total_row = sheet.nrows

# get data from excel, append to the list
data_from_excel = []
for i in range(1, total_row):
    cell_col_1 = sheet.cell_value(i, 0)
    if cell_col_1 == None or cell_col_1 == '':
        break

    cell_col_2 = sheet.cell_value(i, 1)
    cell_col_3 = sheet.cell_value(i, 2)

    row = { 'key' : '{}{}'.format(cell_col_1, cell_col_2),
            'keySwap' : '{}{}'.format(cell_col_2, cell_col_1),
            'name1':'{}'.format(cell_col_1), 
            'name2':'{}'.format(cell_col_2),
            'qty':cell_col_3}
    data_from_excel.append(row)


keys_unique = set(item['key'] for item in data_from_excel)

# keys_swap_unique = set(item['keySwap'] for item in data_from_excel)
# x = keys_unique - keys_swap_unique
# print(keys_swap_unique)
# print(keys_unique)
# print(x)

results = []
for key in keys_unique:
    data_by_key = list(filter(lambda x: (x['key'] == key or x['keySwap'] == key), data_from_excel))
    r = {'key' : '{}'.format(key),
        'qtyMatch' : '{}'.format(len(data_by_key))}
    results.append(r)

print(results)
        