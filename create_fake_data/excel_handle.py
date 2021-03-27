# xlrd 1.2.0 support xlsx
import xlrd
import xlwt
import json

# print dữ liệu theo định dạng json cho dễ nhìn
def jprint(obj):
    text = json.dumps(obj, sort_keys=False, indent=4)
    print(text)
    
print('##### Reading Excel File ######')
# duong dan cua file excel
data_from_excel = []
try:
    loc = ('excel_input/' + 'bben.xlsx')
    wb = xlrd.open_workbook(loc)
    sheet = wb.sheet_by_index(0)
    total_row = sheet.nrows
    # đọc data từ file excel
    for i in range(1, total_row):
        cell_col_1 = sheet.cell_value(i, 0)
        # dòng nào có giá trị = '' thì bỏ qua
        if cell_col_1 == '':
            continue
        cell_col_2 = sheet.cell_value(i, 1)
        cell_col_3 = sheet.cell_value(i, 2)
        # name 1 = A, name2 = B ==> tạo ra 1 cái key = AB và keySwap = BA để lọc dữ liệu cho dễ
        row = { 'key' : '{}{}'.format(cell_col_1, cell_col_2),
                'keySwap' : '{}{}'.format(cell_col_2, cell_col_1),            
                'name1':'{}'.format(cell_col_1), 
                'name2':'{}'.format(cell_col_2),
                'qty':cell_col_3}
        data_from_excel.append(row)
except:
    print('##### Cant Read Excel File ######')

# list key
keys_unique = set(item['key'] for item in data_from_excel)
# list swap key
keys_swap_unique = set(item['keySwap'] for item in data_from_excel)

print('##### Select data ######')
#đếm data
data_list = []
for key in keys_unique:
    x = key
    # nếu cái key đó có trong keySwap list
    # cập nhật lại cái key có keyswap = key ( ví dụ AB == > cập nhật thành BA) cái này là swap key[::-1] AB[::-1] = BA
    if key in keys_swap_unique:
        keys_swap_unique.remove(key[::-1])
        x = key[::-1]
    # list data với điều kiện key = key hoặc key = keySwap ( ví dụ AB hoặc BA sẽ đếm là 2 )
    data_list_by_key = list(filter(lambda x: (x['key'] == key or x['keySwap'] == key), data_from_excel))
    r = {'key' : '{}'.format(x),
        'qtyMatch' : '{}'.format(len(data_list_by_key))}
    data_list.append(r)
    
#xóa bớt 1 phần tử bị trùng
key_unique_new = set(item['key'] for item in data_list)
results = []
for key in key_unique_new:
    item = list(filter(lambda x:(x['key'] == key), data_list))[0]
    results.append(item)
    
#xóa bớt 1 phần tử bị trùng cách 2
# keys = set()
# res = [d for d in data_list if d['key'] not in keys and not keys.add(d['key'])]

# sắp xếp theo key
results = sorted(results, key=lambda k: k.get('key', 0), reverse = False)
print('##### Json Result ######')
jprint(results)

# tao ham export
def export_excel (data):
    try:
        book = xlwt.Workbook()
        sh = book.add_sheet('sheet 1')
        
        sh.write(0, 0, 'Clash')
        sh.write(0, 1, 'Qty Match')
        
        i = 1
        for r in data:
            sh.write(i, 0, r['key'])
            sh.write(i, 1, r['qtyMatch'])
            i = i + 1
        book.save('excel_output/' + 'result.xls')
        return True
    except:
        return False

if export_excel(results) == False:
    print('##### Export Failed ######')
else:
    print('##### Export Excel Successful ######')
    
input('Press ENTER to exit !')

    
