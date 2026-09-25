import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../core/api/api_exception.dart';
import '../../../core/theme/brand.dart';
import '../../../core/widgets/surfaces.dart';
import '../../reference/data/reference_models.dart';
import '../../reports/presentation/providers/report_providers.dart';
import '../data/feed_models.dart';
import '../data/feed_repository.dart';
import 'found_feed_controller.dart';

/// "I found something." What, where, when, and a line the owner would recognise - and
/// nothing that proves ownership. The hint says why: a finder who lists every detail out of
/// helpfulness hands a fraudulent claimant the answers.
class PostFoundPage extends ConsumerStatefulWidget {
  const PostFoundPage({super.key});

  @override
  ConsumerState<PostFoundPage> createState() => _PostFoundPageState();
}

class _PostFoundPageState extends ConsumerState<PostFoundPage> {
  final _form = GlobalKey<FormState>();
  String? _categoryId;
  String? _itemTypeId;
  String? _locationId;
  final _description = TextEditingController();
  final _colour = TextEditingController();
  final _lostCode = TextEditingController();
  DateTime _foundAt = DateTime.now();
  bool _busy = false;
  FoundPost? _posted;

  @override
  void dispose() {
    _description.dispose();
    _colour.dispose();
    _lostCode.dispose();
    super.dispose();
  }

  Future<void> _pickWhen() async {
    final date = await showDatePicker(context: context, initialDate: _foundAt, firstDate: DateTime.now().subtract(const Duration(days: 60)), lastDate: DateTime.now());
    if (date == null || !mounted) return;
    final time = await showTimePicker(context: context, initialTime: TimeOfDay.fromDateTime(_foundAt));
    if (time == null) return;
    setState(() => _foundAt = DateTime(date.year, date.month, date.day, time.hour, time.minute));
  }

  Future<void> _submit() async {
    if (!(_form.currentState?.validate() ?? false)) return;
    setState(() => _busy = true);
    try {
      final post = await ref.read(feedRepositoryProvider).postFound(
            categoryId: _categoryId!,
            itemTypeId: _itemTypeId!,
            foundLocationId: _locationId!,
            description: _description.text.trim(),
            primaryColor: _colour.text,
            foundAt: _foundAt,
            lostReportHandInCode: _lostCode.text.replaceAll(' ', ''),
          );
      if (!mounted) return;
      ref.read(foundFeedControllerProvider.notifier).refresh();
      setState(() => _posted = post);
    } on ApiException catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error.message)));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    final categories = ref.watch(categoriesProvider);
    final locations = ref.watch(campusLocationsProvider);

    if (_posted case final post?) {
      return Scaffold(
        appBar: AppBar(),
        body: ListView(
          padding: const EdgeInsets.fromLTRB(20, 8, 20, 32),
          children: [
            Text('Posted. Now walk it to a desk.', style: text.headlineSmall),
            const SizedBox(height: 8),
            Text(
              'It is on the found board so the owner can spot it, and we are checking it against open reports. '
              'Nobody can claim it until a desk has it - so the sooner it gets there, the sooner it gets home.',
              style: text.bodyMedium?.copyWith(color: Brand.muted, height: 1.45),
            ),
            if (post.handInCode != null) ...[
              const SizedBox(height: 20),
              Container(
                padding: const EdgeInsets.all(18),
                decoration: BoxDecoration(color: Brand.forest, borderRadius: BorderRadius.circular(Brand.radiusCard)),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text('Quote this at the desk', style: TextStyle(color: Colors.white70, fontSize: 12)),
                    Text(displayCode(post.handInCode!),
                        style: const TextStyle(color: Colors.white, fontSize: 34, fontWeight: FontWeight.w600, letterSpacing: 6)),
                  ],
                ),
              ),
            ],
            const SizedBox(height: 24),
            InkButton(label: 'Done', onPressed: () => context.pop()),
          ],
        ),
      );
    }

    return Scaffold(
      appBar: AppBar(title: const Text('Found something?')),
      body: Form(
        key: _form,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(20, 4, 20, 32),
          children: [
            Text('Post it so the owner can spot it, then hand it in at any desk.',
                style: text.bodyMedium?.copyWith(color: Brand.muted, height: 1.45)),
            const SizedBox(height: 20),
            categories.when(
              loading: () => const LinearProgressIndicator(),
              error: (_, __) => const Text('Could not load categories.'),
              data: (items) => Column(
                children: [
                  DropdownButtonFormField<String>(
                    initialValue: _categoryId,
                    decoration: const InputDecoration(labelText: 'What is it?'),
                    items: [for (final c in items) DropdownMenuItem(value: c.id, child: Text(c.name))],
                    onChanged: (v) => setState(() { _categoryId = v; _itemTypeId = null; }),
                    validator: (v) => v == null ? 'Choose a category.' : null,
                  ),
                  const SizedBox(height: 12),
                  DropdownButtonFormField<String>(
                    key: ValueKey(_categoryId),
                    initialValue: _itemTypeId,
                    decoration: const InputDecoration(labelText: 'Item type'),
                    items: [
                      for (final t in items.firstWhere((c) => c.id == _categoryId, orElse: () => const CategoryModel(id: '', name: '', description: null, isHighlighted: false, itemTypes: [])).itemTypes)
                        DropdownMenuItem(value: t.id, child: Text(t.name)),
                    ],
                    onChanged: _categoryId == null ? null : (v) => setState(() => _itemTypeId = v),
                    validator: (v) => v == null ? 'Choose an item type.' : null,
                  ),
                ],
              ),
            ),
            const SizedBox(height: 12),
            locations.when(
              loading: () => const SizedBox.shrink(),
              error: (_, __) => const Text('Could not load locations.'),
              data: (items) => DropdownButtonFormField<String>(
                initialValue: _locationId,
                decoration: const InputDecoration(labelText: 'Where did you find it?'),
                items: [for (final l in items) DropdownMenuItem(value: l.id, child: Text(l.name))],
                onChanged: (v) => setState(() => _locationId = v),
                validator: (v) => v == null ? 'Choose a location.' : null,
              ),
            ),
            const SizedBox(height: 12),
            OutlinedButton.icon(
              onPressed: _pickWhen,
              icon: const Icon(Icons.schedule_outlined, size: 18),
              label: Text(DateFormat('EEE, d MMM · h:mm a').format(_foundAt)),
            ),
            const SizedBox(height: 12),
            TextFormField(controller: _colour, decoration: const InputDecoration(labelText: 'Main colour', hintText: 'Grey')),
            const SizedBox(height: 12),
            TextFormField(
              controller: _description,
              minLines: 3,
              maxLines: 5,
              decoration: const InputDecoration(
                labelText: 'A line the owner would recognise',
                hintText: 'Grey water bottle with stickers, on a bench outside the gym.',
                helperText: 'Leave out anything that proves it is theirs - a name inside, what is in the pockets. The desk records that.',
                helperMaxLines: 3,
              ),
              validator: (v) => (v ?? '').trim().length < 10 ? 'Say what it is and roughly where.' : null,
            ),
            const SizedBox(height: 20),
            Panel(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Did you spot the owner\'s post?', style: text.titleSmall),
                  const SizedBox(height: 4),
                  Text('If you already found the matching lost post, type its six-digit code and the owner is told straight away.',
                      style: text.bodySmall?.copyWith(color: Brand.muted, height: 1.4)),
                  const SizedBox(height: 10),
                  TextFormField(
                    controller: _lostCode,
                    keyboardType: TextInputType.number,
                    maxLength: 7,
                    decoration: const InputDecoration(hintText: '483 921', counterText: ''),
                    style: const TextStyle(fontSize: 20, letterSpacing: 4, fontFeatures: [FontFeature.tabularFigures()]),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 24),
            InkButton(label: 'Post it', icon: Icons.front_hand_outlined, busy: _busy, onPressed: _submit),
          ],
        ),
      ),
    );
  }
}
