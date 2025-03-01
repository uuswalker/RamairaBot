# RamairaBot Namespaces dan Kegunaan

## RamairaBot.AI
### PPOModel
PPOModel adalah kelas yang mengimplementasikan model Proximal Policy Optimization (PPO) untuk agen pembelajaran mesin dalam permainan. Model ini bertanggung jawab untuk memprediksi tindakan yang akan diambil oleh agen berdasarkan input lingkungan.

### PPOTrainer
PPOTrainer adalah kelas yang bertanggung jawab untuk melatih model Proximal Policy Optimization (PPO). Ini termasuk proses pembaruan parameter model berdasarkan data pelatihan yang dikumpulkan dari lingkungan permainan.

### ActionSelector
ActionSelector adalah kelas yang bertanggung jawab untuk memilih tindakan yang akan diambil oleh agen berdasarkan prediksi yang dibuat oleh model PPO. Ini termasuk logika untuk eksplorasi dan eksploitasi.

## RamairaBot.BotLogic
### Pathfinding
Pathfinding adalah kelas yang mengimplementasikan algoritma pathfinding seperti A* untuk menentukan jalur terbaik yang harus ditempuh oleh agen dalam lingkungan permainan.

### SteeringBehavior
SteeringBehavior adalah kelas yang mengimplementasikan berbagai perilaku steering (mengemudi) seperti seek, flee, arrive, dan lain-lain yang digunakan untuk mengatur pergerakan agen dalam permainan.

### ActionExecutor
ActionExecutor adalah kelas yang bertanggung jawab untuk mengeksekusi tindakan yang dipilih oleh agen, termasuk pergerakan, serangan, dan interaksi lainnya dalam permainan.

## RamairaBot.GameInterface
### GamePlugin
GamePlugin adalah kelas yang mengatur integrasi antara bot dan permainan. Ini termasuk metode untuk mengontrol langsung dari permainan dan berinteraksi dengan API permainan.

### InputProcessor
InputProcessor adalah kelas yang bertanggung jawab untuk memproses input dari permainan, termasuk data lingkungan yang akan digunakan oleh model AI untuk membuat keputusan.

### FeedbackProcessor
FeedbackProcessor adalah kelas yang memproses feedback atau umpan balik dari permainan yang digunakan untuk memperbaiki atau memperbarui model AI.

## RamairaBot.Input
### KeyboardInput
KeyboardInput adalah kelas yang menangani input dari keyboard untuk mengontrol bot. Ini termasuk metode untuk menangkap dan memproses input keyboard.

### MouseInput
MouseInput adalah kelas yang menangani input dari mouse, seperti mengarahkan dan mengklik, untuk mengontrol bot dalam permainan.

## RamairaBot.Output
### MovementOutput
MovementOutput adalah kelas yang menghasilkan perintah gerakan untuk bot berdasarkan keputusan yang dibuat oleh model AI.

### CombatOutput
CombatOutput adalah kelas yang menghasilkan perintah tempur, seperti menembak atau bertahan, berdasarkan keputusan yang dibuat oleh model AI.

## RamairaBot
### PPOCommunicator
PPOCommunicator adalah kelas yang mengatur komunikasi antara bot dan agen PPO eksternal untuk pertukaran data dan instruksi.

### DataFormatter
DataFormatter adalah kelas yang bertanggung jawab untuk memformat data dari permainan menjadi format yang dapat digunakan oleh model AI.

### ActionInterpreter
ActionInterpreter adalah kelas yang menginterpretasikan aksi yang dihasilkan oleh model AI menjadi perintah yang dapat dieksekusi dalam permainan.

## RamairaBot.Tests.CoreTests
### PPOModelTests
PPOModelTests adalah kelas yang mengimplementasikan unit test untuk menguji fungsionalitas dari kelas PPOModel.

### PathfindingTests
PathfindingTests adalah kelas yang mengimplementasikan unit test untuk menguji fungsionalitas dari kelas Pathfinding.

## RamairaBot.Tests.PluginTests
### InputProcessorTests
InputProcessorTests adalah kelas yang mengimplementasikan unit test untuk menguji fungsionalitas dari kelas InputProcessor.

### ActionExecutorTests
ActionExecutorTests adalah kelas yang mengimplementasikan unit test untuk menguji fungsionalitas dari kelas ActionExecutor.