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
        row = { 'key' : '{}-{}'.format(cell_col_1, cell_col_2),
                'keySwap' : '{}-{}'.format(cell_col_2, cell_col_1),            
                'name1':'{}'.format(cell_col_1), 
                'name2':'{}'.format(cell_col_2),
                'qty':cell_col_3}
        data_from_excel.append(row)
except:
    print('##### Cant Read Excel File ######')

#print('##### Data from excel ######')
#print(data_from_excel)

# Reading txt file
raw_txt = []
try:
    file_open = open("txt_input/" + "Input.txt")
    file_content = file_open.readlines()
    for line in file_content:
        raw_txt.append(line.strip())
except:
    print('##### Cant read txt ######')

# hàm lấy name1 hoặc name2 từ 1 dòng trong file txt
def read_content(item):
    # tách content giữa dấu : ( ví dụ path:C111 tách thành path và C111 )
    if ":" in item:
        item_split = item.split(":")
        return item_split[1].strip()
    else:
        return "####"
    

# Lọc data từ file txt
index = 0
data_from_txt = []
for item in raw_txt:
    # đọc theo dòng. nếu dòng đó = item 1 thì vào lấy data.
    if item.strip().upper() == "ITEM 1":
        next_item1 = raw_txt[index + 1].strip().replace("\t","")
        name1 = read_content(next_item1)
        
        # tìm name2
        index_name2 = index + 1
        while raw_txt[index_name2].strip().upper() != "ITEM 2":
            index_name2 += 1
        next_item2 = raw_txt[index_name2 + 1].strip().replace("\t","")
        name2 = read_content(next_item2)
        # tạo 1 dòng mới
        row = { 'key' : '{}!{}'.format(name1, name2),
                'keySwap' : '{}!{}'.format(name2, name1),
                'name1':'{}'.format(name1), 
                'name2':'{}'.format(name2),
                'qty': 1}
        data_from_txt.append(row)
    index = index + 1

print('##### Data from txt ######')
jprint(data_from_txt)

data_from_excel = data_from_txt
# list key
keys_unique = set(item['key'] for item in data_from_excel)

# list swap key
keys_swap_unique = set(item['keySwap'] for item in data_from_excel)

print(keys_unique)
print(keys_swap_unique)

print('##### Select data ######')
#đếm data
data_list = []
for key in keys_unique:
    x = key
    # nếu cái key đó có trong keySwap list
    # cập nhật lại cái key có keyswap = key ( ví dụ AB == > cập nhật thành BA) cái này là swap key[::-1] AB[::-1] = BA
    # cập nhật dùng split để swap thay cho đảo ký tự [::-1] ( ví dụ ACBE!QET swap thành QET!ACBE )
    keys_split = key.split("!")
    key_swap_temp = keys_split[1] + "!" + keys_split[0]
    if key in keys_swap_unique:
        keys_swap_unique.remove(key_swap_temp)
        x = key_swap_temp
    # list data với điều kiện key = key hoặc key = keySwap ( ví dụ AB hoặc BA sẽ đếm là 2 )
    data_list_by_key = list(filter(lambda x: (x['key'] == key or x['keySwap'] == key), data_from_excel))
    r = {'key' : '{}'.format(x),
         'name1' : '{}'.format(data_list_by_key[0]['name1']),
         'name2' : '{}'.format(data_list_by_key[0]['name2']),
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
        sh.write(0, 1, 'Name1')
        sh.write(0, 2, 'Name2')
        sh.write(0, 3, 'Qty Match')
        
        i = 1
        for r in data:
            #sh.write(i, 0, r['key'])
            sh.write(i, 0, r['name1'] + " -> " + r['name2'])
            sh.write(i, 1, r['name1'])
            sh.write(i, 2, r['name2'])
            sh.write(i, 3, r['qtyMatch'])
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

    
