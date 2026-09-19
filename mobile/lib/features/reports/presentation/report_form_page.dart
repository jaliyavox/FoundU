import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:image_picker/image_picker.dart';
import 'package:intl/intl.dart';

import '../../reference/data/reference_models.dart';
import '../data/report_models.dart';
import 'providers/report_providers.dart';

class ReportFormPage extends ConsumerStatefulWidget {
  final String? reportId; // null for Create mode, non-null for Edit mode

  const ReportFormPage({super.key, this.reportId});

  @override
  ConsumerState<ReportFormPage> createState() => _ReportFormPageState();
}

class _ReportFormPageState extends ConsumerState<ReportFormPage> {
  final _formKey = GlobalKey<FormState>();

  String? _selectedCategoryId;
  String? _selectedItemTypeId;
  String? _selectedLocationId;

  final TextEditingController _descriptionController = TextEditingController();
  final TextEditingController _primaryColorController = TextEditingController();
  final TextEditingController _secondaryColorController = TextEditingController();

  DateTime _lostFromAt = DateTime.now().subtract(const Duration(hours: 2));
  DateTime _lostToAt = DateTime.now();

  final List<XFile> _pickedImages = [];
  final ImagePicker _picker = ImagePicker();

  bool _isInitialDataLoaded = false;

  @override
  void dispose() {
    _descriptionController.dispose();
    _primaryColorController.dispose();
    _secondaryColorController.dispose();
    super.dispose();
  }

  void _loadExistingReport(LostReportDetailModel report) {
    if (_isInitialDataLoaded) return;
    _isInitialDataLoaded = true;

    _selectedCategoryId = report.categoryId;
    _selectedItemTypeId = report.itemTypeId;
    _selectedLocationId = report.lastSeenLocationId;
    _descriptionController.text = report.description;
    _primaryColorController.text = report.primaryColor ?? '';
    _secondaryColorController.text = report.secondaryColor ?? '';
    _lostFromAt = report.estimatedLostFromAt.toLocal();
    _lostToAt = report.estimatedLostToAt.toLocal();
  }

  Future<void> _pickImage(ImageSource source) async {
    try {
      final picked = await _picker.pickImage(
        source: source,
        maxWidth: 1600,
        maxHeight: 1600,
        imageQuality: 85,
      );
      if (picked != null) {
        setState(() {
          _pickedImages.add(picked);
        });
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Failed to pick image: $e')),
        );
      }
    }
  }

  void _applyTimePreset(String preset) {
    final now = DateTime.now();
    setState(() {
      if (preset == '2h') {
        _lostFromAt = now.subtract(const Duration(hours: 2));
        _lostToAt = now;
      } else if (preset == 'today') {
        _lostFromAt = DateTime(now.year, now.month, now.day, 0, 0);
        _lostToAt = now;
      } else if (preset == 'yesterday') {
        final yesterday = now.subtract(const Duration(days: 1));
        _lostFromAt = DateTime(yesterday.year, yesterday.month, yesterday.day, 0, 0);
        _lostToAt = DateTime(yesterday.year, yesterday.month, yesterday.day, 23, 59);
      }
    });
  }

  Future<void> _selectDateTime(BuildContext context, bool isFrom) async {
    final initialDate = isFrom ? _lostFromAt : _lostToAt;
    final pickedDate = await showDatePicker(
      context: context,
      initialDate: initialDate,
      firstDate: DateTime.now().subtract(const Duration(days: 90)),
      lastDate: DateTime.now(),
      builder: (context, child) {
        return Theme(
          data: Theme.of(context).copyWith(
            colorScheme: const ColorScheme.light(primary: Color(0xFF2E7D32)),
          ),
          child: child!,
        );
      },
    );

    if (pickedDate == null || !mounted) return;

    final pickedTime = await showTimePicker(
      context: context,
      initialTime: TimeOfDay.fromDateTime(initialDate),
      builder: (context, child) {
        return Theme(
          data: Theme.of(context).copyWith(
            colorScheme: const ColorScheme.light(primary: Color(0xFF2E7D32)),
          ),
          child: child!,
        );
      },
    );

    if (pickedTime == null || !mounted) return;

    final selected = DateTime(
      pickedDate.year,
      pickedDate.month,
      pickedDate.day,
      pickedTime.hour,
      pickedTime.minute,
    );

    setState(() {
      if (isFrom) {
        _lostFromAt = selected;
        if (_lostToAt.isBefore(_lostFromAt)) {
          _lostToAt = _lostFromAt.add(const Duration(hours: 1));
        }
      } else {
        _lostToAt = selected;
      }
    });
  }

  Future<void> _submitForm() async {
    if (!_formKey.currentState!.validate()) return;
    if (_selectedCategoryId == null || _selectedCategoryId!.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Please select a Category')),
      );
      return;
    }
    if (_selectedItemTypeId == null || _selectedItemTypeId!.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Please select an Item Type')),
      );
      return;
    }
    if (_selectedLocationId == null || _selectedLocationId!.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Please select a Last-Seen Location')),
      );
      return;
    }

    final isEdit = widget.reportId != null;

    try {
      if (isEdit) {
        final request = UpdateLostReportRequest(
          categoryId: _selectedCategoryId!,
          itemTypeId: _selectedItemTypeId!,
          lastSeenLocationId: _selectedLocationId!,
          description: _descriptionController.text.trim(),
          primaryColor: _primaryColorController.text.trim().isEmpty
              ? null
              : _primaryColorController.text.trim(),
          secondaryColor: _secondaryColorController.text.trim().isEmpty
              ? null
              : _secondaryColorController.text.trim(),
          estimatedLostFromAt: _lostFromAt,
          estimatedLostToAt: _lostToAt,
        );
        await ref.read(reportControllerProvider.notifier).updateReport(
              reportId: widget.reportId!,
              request: request,
              newImages: _pickedImages,
            );
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('Lost report updated successfully!')),
          );
          context.pop();
        }
      } else {
        final request = CreateLostReportRequest(
          categoryId: _selectedCategoryId!,
          itemTypeId: _selectedItemTypeId!,
          lastSeenLocationId: _selectedLocationId!,
          description: _descriptionController.text.trim(),
          primaryColor: _primaryColorController.text.trim().isEmpty
              ? null
              : _primaryColorController.text.trim(),
          secondaryColor: _secondaryColorController.text.trim().isEmpty
              ? null
              : _secondaryColorController.text.trim(),
          estimatedLostFromAt: _lostFromAt,
          estimatedLostToAt: _lostToAt,
        );
        final created = await ref
            .read(reportControllerProvider.notifier)
            .createReport(request: request, images: _pickedImages);

        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('Lost report created successfully!')),
          );
          if (created != null) {
            context.go('/reports/${created.id}');
          } else {
            context.pop();
          }
        }
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Failed: ${e.toString()}'),
            backgroundColor: Colors.red[700],
          ),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final isEdit = widget.reportId != null;
    final categoriesAsync = ref.watch(categoriesProvider);
    final locationsAsync = ref.watch(campusLocationsProvider);
    final controllerState = ref.watch(reportControllerProvider);

    // If editing, watch existing report
    if (isEdit) {
      ref.listen<AsyncValue<LostReportDetailModel>>(
        reportDetailProvider(widget.reportId!),
        (_, next) {
          next.whenData((report) => _loadExistingReport(report));
        },
      );

      final detailAsync = ref.watch(reportDetailProvider(widget.reportId!));
      detailAsync.whenData((report) => _loadExistingReport(report));
    }

    final dateFormat = DateFormat('MMM d, yyyy  h:mm a');

    return Scaffold(
      appBar: AppBar(
        title: Text(
          isEdit ? 'Edit Lost Report' : 'Report Lost Item',
          style: const TextStyle(fontWeight: FontWeight.bold),
        ),
        elevation: 0,
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(20),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              // 1. Category & Item Type Selection Section
              _buildSectionHeader('1. What did you lose?', Icons.category_outlined),
              const SizedBox(height: 12),
              categoriesAsync.when(
                loading: () => const LinearProgressIndicator(color: Color(0xFF2E7D32)),
                error: (err, _) => Text('Error loading categories: $err', style: const TextStyle(color: Colors.red)),
                data: (categories) {
                  final selectedCategory = categories.firstWhere(
                    (c) => c.id == _selectedCategoryId,
                    orElse: () => categories.isNotEmpty ? categories.first : categories[0],
                  );

                  // If category selected, get item types
                  final itemTypes = _selectedCategoryId != null ? selectedCategory.itemTypes : <ItemTypeModel>[];

                  return Column(
                    children: [
                      DropdownButtonFormField<String>(
                        initialValue: _selectedCategoryId,
                        decoration: _inputDecoration('Category *', Icons.grid_view),
                        items: categories.map((cat) {
                          return DropdownMenuItem(
                            value: cat.id,
                            child: Text(cat.name),
                          );
                        }).toList(),
                        onChanged: (val) {
                          setState(() {
                            _selectedCategoryId = val;
                            _selectedItemTypeId = null; // reset item type
                          });
                        },
                        validator: (val) => val == null ? 'Please select a category' : null,
                      ),
                      const SizedBox(height: 16),
                      DropdownButtonFormField<String>(
                        initialValue: _selectedItemTypeId,
                        decoration: _inputDecoration('Item Type *', Icons.merge_type),
                        items: itemTypes.map((type) {
                          return DropdownMenuItem(
                            value: type.id,
                            child: Text(type.name),
                          );
                        }).toList(),
                        onChanged: (val) {
                          setState(() => _selectedItemTypeId = val);
                        },
                        validator: (val) => val == null ? 'Please select an item type' : null,
                      ),
                    ],
                  );
                },
              ),
              const SizedBox(height: 24),

              // 2. Last Seen Location Section
              _buildSectionHeader('2. Where was it last seen?', Icons.location_on_outlined),
              const SizedBox(height: 12),
              locationsAsync.when(
                loading: () => const LinearProgressIndicator(color: Color(0xFF2E7D32)),
                error: (err, _) => Text('Error loading locations: $err', style: const TextStyle(color: Colors.red)),
                data: (locations) {
                  return DropdownButtonFormField<String>(
                    initialValue: _selectedLocationId,
                    decoration: _inputDecoration('Campus Location *', Icons.place_outlined),
                    items: locations.map((loc) {
                      final title = loc.building != null && loc.building!.isNotEmpty
                          ? '${loc.name} (${loc.building})'
                          : loc.name;
                      return DropdownMenuItem(
                        value: loc.id,
                        child: Text(title, overflow: TextOverflow.ellipsis),
                      );
                    }).toList(),
                    onChanged: (val) => setState(() => _selectedLocationId = val),
                    validator: (val) => val == null ? 'Please select a location' : null,
                  );
                },
              ),
              const SizedBox(height: 24),

              // 3. Date & Time Window Section
              _buildSectionHeader('3. When was it lost?', Icons.access_time_outlined),
              const SizedBox(height: 10),
              // Presets row
              Row(
                children: [
                  const Text('Quick Select: ', style: TextStyle(fontSize: 13, color: Colors.grey)),
                  const SizedBox(width: 8),
                  _presetChip('Last 2 Hours', () => _applyTimePreset('2h')),
                  const SizedBox(width: 6),
                  _presetChip('Today', () => _applyTimePreset('today')),
                  const SizedBox(width: 6),
                  _presetChip('Yesterday', () => _applyTimePreset('yesterday')),
                ],
              ),
              const SizedBox(height: 12),
              Card(
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                color: Theme.of(context).colorScheme.surfaceContainerHighest.withValues(alpha: 0.3),
                elevation: 0,
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Column(
                    children: [
                      ListTile(
                        dense: true,
                        leading: const Icon(Icons.date_range, color: Color(0xFF2E7D32)),
                        title: const Text('Lost From', style: TextStyle(fontSize: 12, color: Colors.grey)),
                        subtitle: Text(
                          dateFormat.format(_lostFromAt),
                          style: const TextStyle(fontSize: 14, fontWeight: FontWeight.bold),
                        ),
                        trailing: const Icon(Icons.edit_calendar, size: 20),
                        onTap: () => _selectDateTime(context, true),
                      ),
                      const Divider(height: 1),
                      ListTile(
                        dense: true,
                        leading: const Icon(Icons.event, color: Color(0xFF2E7D32)),
                        title: const Text('Lost Until (Estimated)', style: TextStyle(fontSize: 12, color: Colors.grey)),
                        subtitle: Text(
                          dateFormat.format(_lostToAt),
                          style: const TextStyle(fontSize: 14, fontWeight: FontWeight.bold),
                        ),
                        trailing: const Icon(Icons.edit_calendar, size: 20),
                        onTap: () => _selectDateTime(context, false),
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 24),

              // 4. Description & Attributes
              _buildSectionHeader('4. Description & Details', Icons.description_outlined),
              const SizedBox(height: 12),
              TextFormField(
                controller: _descriptionController,
                minLines: 3,
                maxLines: 5,
                decoration: _inputDecoration(
                  'Detailed Description *',
                  Icons.notes,
                  hint: 'Describe distinct markings, stickers, serial numbers, brand, scratches...',
                ),
                validator: (val) {
                  if (val == null || val.trim().isEmpty) {
                    return 'Please enter a description';
                  }
                  if (val.trim().length < 10) {
                    return 'Please provide at least 10 characters of detail';
                  }
                  return null;
                },
              ),
              const SizedBox(height: 16),
              Row(
                children: [
                  Expanded(
                    child: TextFormField(
                      controller: _primaryColorController,
                      decoration: _inputDecoration('Primary Color', Icons.palette_outlined),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: TextFormField(
                      controller: _secondaryColorController,
                      decoration: _inputDecoration('Secondary Color', Icons.color_lens_outlined),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 24),

              // 5. Image Picker / Photos
              _buildSectionHeader('5. Upload Photos (Optional)', Icons.camera_alt_outlined),
              const SizedBox(height: 8),
              const Text(
                'Upload clear photos of your lost item or reference images to assist matching.',
                style: TextStyle(fontSize: 12, color: Colors.grey),
              ),
              const SizedBox(height: 12),

              Row(
                children: [
                  OutlinedButton.icon(
                    onPressed: () => _pickImage(ImageSource.camera),
                    icon: const Icon(Icons.camera_alt, size: 18),
                    label: const Text('Take Photo'),
                  ),
                  const SizedBox(width: 12),
                  OutlinedButton.icon(
                    onPressed: () => _pickImage(ImageSource.gallery),
                    icon: const Icon(Icons.photo_library, size: 18),
                    label: const Text('From Gallery'),
                  ),
                ],
              ),
              const SizedBox(height: 12),

              if (_pickedImages.isNotEmpty)
                SizedBox(
                  height: 90,
                  child: ListView.builder(
                    scrollDirection: Axis.horizontal,
                    itemCount: _pickedImages.length,
                    itemBuilder: (context, index) {
                      final image = _pickedImages[index];
                      return Stack(
                        children: [
                          Container(
                            margin: const EdgeInsets.only(right: 12, top: 4),
                            width: 80,
                            height: 80,
                            decoration: BoxDecoration(
                              borderRadius: BorderRadius.circular(10),
                              image: DecorationImage(
                                image: FileImage(File(image.path)),
                                fit: BoxFit.cover,
                              ),
                            ),
                          ),
                          Positioned(
                            top: 0,
                            right: 8,
                            child: GestureDetector(
                              onTap: () {
                                setState(() {
                                  _pickedImages.removeAt(index);
                                });
                              },
                              child: Container(
                                decoration: const BoxDecoration(
                                  color: Colors.red,
                                  shape: BoxShape.circle,
                                ),
                                padding: const EdgeInsets.all(4),
                                child: const Icon(Icons.close, size: 14, color: Colors.white),
                              ),
                            ),
                          ),
                        ],
                      );
                    },
                  ),
                ),
              const SizedBox(height: 32),

              // Submit Button
              ElevatedButton(
                onPressed: controllerState.isLoading ? null : _submitForm,
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF2E7D32),
                  foregroundColor: Colors.white,
                  padding: const EdgeInsets.symmetric(vertical: 16),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                  elevation: 2,
                ),
                child: controllerState.isLoading
                    ? const SizedBox(
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                      )
                    : Text(
                        isEdit ? 'Save Changes' : 'Submit Lost Report',
                        style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                      ),
              ),
              const SizedBox(height: 24),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildSectionHeader(String title, IconData icon) {
    return Row(
      children: [
        Icon(icon, size: 20, color: const Color(0xFF2E7D32)),
        const SizedBox(width: 8),
        Text(
          title,
          style: const TextStyle(
            fontSize: 16,
            fontWeight: FontWeight.bold,
            color: Color(0xFF1E5631),
          ),
        ),
      ],
    );
  }

  Widget _presetChip(String label, VoidCallback onTap) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(16),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
        decoration: BoxDecoration(
          color: const Color(0xFFE8F5E9),
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: const Color(0xFFA5D6A7)),
        ),
        child: Text(
          label,
          style: const TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: Color(0xFF2E7D32)),
        ),
      ),
    );
  }

  InputDecoration _inputDecoration(String label, IconData icon, {String? hint}) {
    return InputDecoration(
      labelText: label,
      hintText: hint,
      prefixIcon: Icon(icon, size: 20, color: const Color(0xFF2E7D32)),
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
    );
  }
}
